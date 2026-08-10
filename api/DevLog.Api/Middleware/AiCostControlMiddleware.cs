using DevLog.Api.Options;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace DevLog.Api.Middleware;

public class AiCostControlMiddleware : IAsyncDisposable
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<AiCostControlOptions> _options;
    private readonly FixedWindowRateLimiter _globalLimiter;

    public AiCostControlMiddleware(
        RequestDelegate next,
        IOptionsMonitor<AiCostControlOptions> options)
    {
        _next = next;
        _options = options;

        var permitLimit = Math.Max(1, options.CurrentValue.GlobalExpensiveRequestsPerMinute);
        _globalLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    public async Task InvokeAsync(HttpContext context)
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
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "ai_temporarily_disabled",
                reason = "kill_switch_enabled"
            }, cancellationToken: context.RequestAborted);
            return;
        }

        using var lease = await _globalLimiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
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

    public ValueTask DisposeAsync() => _globalLimiter.DisposeAsync();

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
}
