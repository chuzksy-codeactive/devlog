using DevLog.Infrastructure.BetaAccess;
using DevLog.Api.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace DevLog.Api.Middleware;

/// <summary>
/// Blocks protected API endpoints for users who are not allowed into the private beta.
/// </summary>
public class PrivateBetaAccessMiddleware
{
    private readonly RequestDelegate _next;

    public PrivateBetaAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Evaluates beta eligibility for authorized API endpoints and returns explicit deny reasons.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        IBetaAccessService betaAccessService,
        ILogger<PrivateBetaAccessMiddleware>? logger = null)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/beta-access/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
        {
            await _next(context);
            return;
        }

        var result = await betaAccessService.EvaluateCurrentUserAsync(context.RequestAborted);
        if (result.Outcome is BetaAccessOutcome.Allowed or BetaAccessOutcome.BetaDisabled)
        {
            await _next(context);
            return;
        }

        if (result.Outcome == BetaAccessOutcome.Unauthenticated)
        {
            SecurityTelemetry.TrackPrivateBetaDeny(
                reasonCode: "unauthenticated",
                path: context.Request.Path.Value ?? "unknown");
            logger?.LogWarning(
                "Private beta denied due to unauthenticated request. Path: {Path}, Method: {Method}",
                context.Request.Path.Value,
                context.Request.Method);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "authentication_required",
                reason = "unauthenticated"
            }, cancellationToken: context.RequestAborted);
            return;
        }

        var reasonCode = ToReasonCode(result.Outcome);
        SecurityTelemetry.TrackPrivateBetaDeny(
            reasonCode: reasonCode,
            path: context.Request.Path.Value ?? "unknown");
        logger?.LogWarning(
            "Private beta denied. Reason: {Reason}, Path: {Path}, Method: {Method}",
            reasonCode,
            context.Request.Path.Value,
            context.Request.Method);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "private_beta_access_denied",
            reason = reasonCode
        }, cancellationToken: context.RequestAborted);
    }

    private static string ToReasonCode(BetaAccessOutcome outcome) =>
        outcome switch
        {
            BetaAccessOutcome.MissingEmailClaim => "missing_email_claim",
            BetaAccessOutcome.EmailNotVerified => "email_not_verified",
            BetaAccessOutcome.NotInvited => "not_invited",
            BetaAccessOutcome.InvitePending => "invite_pending",
            BetaAccessOutcome.InviteExpired => "invite_expired",
            BetaAccessOutcome.InviteRevoked => "invite_revoked",
            _ => "access_denied"
        };
}
