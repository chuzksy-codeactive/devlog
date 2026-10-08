using DevLog.Infrastructure.Ai.CostControl;

namespace DevLog.Api.Middleware;

public class AiQuotaExceptionMiddleware(RequestDelegate next, ILogger<AiQuotaExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AiQuotaRejectedException ex) when (!context.Response.HasStarted)
        {
            logger.LogWarning("AI request denied by durable allowance: {Reason}", ex.Reason);
            await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "rate_limited", ex.Reason);
        }
        catch (AiQuotaPausedException) when (!context.Response.HasStarted)
        {
            logger.LogWarning("AI provider request blocked by kill switch.");
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable,
                "ai_temporarily_disabled", "kill_switch_enabled");
        }
        catch (AiQuotaUnavailableException ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "AI request blocked because durable allowance enforcement is unavailable.");
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable,
                "ai_temporarily_disabled", "quota_store_unavailable");
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int status, string error, string reason)
    {
        context.Response.ContentLength = null;
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { error, reason }, context.RequestAborted);
    }
}
