using DevLog.Infrastructure.BetaAccess;
using Microsoft.AspNetCore.Authorization;

namespace DevLog.Api.Middleware;

public class PrivateBetaAccessMiddleware
{
    private readonly RequestDelegate _next;

    public PrivateBetaAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IBetaAccessService betaAccessService)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
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
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "authentication_required",
                reason = "unauthenticated"
            }, cancellationToken: context.RequestAborted);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "private_beta_access_denied",
            reason = ToReasonCode(result.Outcome)
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
