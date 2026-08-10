using DevLog.Api.CostControl;
using DevLog.Api.Observability;
using DevLog.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevLog.Api.Middleware;

/// <summary>
/// Applies kill-switch and global emergency throttling to expensive AI routes.
/// </summary>
public class AiCostControlMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<AiCostControlOptions> _options;
    private readonly IGlobalAiRequestCapService _globalCapService;

    public AiCostControlMiddleware(
        RequestDelegate next,
        IOptionsMonitor<AiCostControlOptions> options,
        IGlobalAiRequestCapService globalCapService)
    {
        _next = next;
        _options = options;
        _globalCapService = globalCapService;
    }

    /// <summary>
    /// Enforces global AI protection rules before endpoint-specific rate limiter policies run.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ILogger<AiCostControlMiddleware>? logger = null)
    {
        if (!IsExpensiveAiEndpoint(context.Request))
        {
            await _next(context);
            return;
        }

        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        if (options.KillSwitchEnabled)
        {
            var endpointName = NormalizeEndpoint(context.Request);
            SecurityTelemetry.TrackAiKillSwitchBlock(endpointName);
            logger?.LogWarning(
                "AI request blocked by kill switch. Endpoint: {Endpoint}, Path: {Path}",
                endpointName,
                context.Request.Path.Value);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "ai_temporarily_disabled",
                reason = "kill_switch_enabled"
            }, cancellationToken: context.RequestAborted);
            return;
        }

        var acquired = await _globalCapService.TryAcquireAsync(
            scope: "expensive",
            maxPerMinute: options.GlobalExpensiveRequestsPerMinute,
            cancellationToken: context.RequestAborted);
        if (!acquired)
        {
            var endpointName = NormalizeEndpoint(context.Request);
            SecurityTelemetry.TrackAiGlobalCapBlock(endpointName, options.GlobalExpensiveRequestsPerMinute);
            logger?.LogWarning(
                "AI request blocked by global cap. Endpoint: {Endpoint}, Path: {Path}, MaxPerMinute: {MaxPerMinute}",
                endpointName,
                context.Request.Path.Value,
                options.GlobalExpensiveRequestsPerMinute);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "rate_limited",
                reason = "global_emergency_cap_reached"
            }, cancellationToken: context.RequestAborted);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Checks whether the request targets a high-cost AI endpoint.
    /// </summary>
    private static bool IsExpensiveAiEndpoint(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.Equals("/api/onboarding/resume-import", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/api/portfolio/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/ask", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith("/api/entries/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/sanitize", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEndpoint(HttpRequest request)
    {
        var path = request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unknown";
        }

        if (path.Equals("/api/onboarding/resume-import", StringComparison.OrdinalIgnoreCase))
        {
            return "resume_import";
        }

        if (path.StartsWith("/api/portfolio/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/ask", StringComparison.OrdinalIgnoreCase))
        {
            return "ask_ai";
        }

        if (path.StartsWith("/api/entries/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/sanitize", StringComparison.OrdinalIgnoreCase))
        {
            return "sanitize_entry";
        }

        return "other";
    }
}
