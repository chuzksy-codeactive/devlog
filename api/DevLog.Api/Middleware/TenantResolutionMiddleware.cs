using DevLog.Infrastructure.MultiTenancy;

namespace DevLog.Api.Middleware;

// Runs after authentication, before authorization/endpoints. Originally
// read a custom "tenant_id" JWT claim — but nothing anywhere ever
// configured Clerk to issue that claim (that would need a webhook writing
// to Clerk user metadata plus a JWT template edit, deliberately not built —
// see Milestone 7's writeup), so it was always empty for a real token.
// Reads Clerk's standard "sub" claim instead (always present on any valid
// token) and auto-provisions a Tenant + placeholder Portfolio on first
// sight via TenantProvisioningService, rather than requiring a separate
// signup step. Populates CurrentTenantAccessor, which
// TenantConnectionInterceptor reads to set the RLS session variable on the
// next connection this request opens.
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor,
        TenantProvisioningService tenantProvisioningService)
    {
        var clerkUserId = context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(clerkUserId) && tenantAccessor is CurrentTenantAccessor accessor)
        {
            var tenant = await tenantProvisioningService.GetOrCreateTenantAsync(clerkUserId, context.RequestAborted);
            accessor.TenantId = tenant.Id;
        }

        await _next(context);
    }
}
