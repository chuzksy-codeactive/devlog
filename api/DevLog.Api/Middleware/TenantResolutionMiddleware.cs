using DevLog.Infrastructure.MultiTenancy;

namespace DevLog.Api.Middleware;

// Runs after authentication, before authorization/endpoints. Reads tenant_id
// from the verified JWT claim only — this is the "never from client input
// directly" rule from architecture doc Section 4. Populates
// CurrentTenantAccessor, which TenantConnectionInterceptor reads to set the
// RLS session variable on the next connection this request opens.
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor)
    {
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(tenantClaim, out var tenantId) && tenantAccessor is CurrentTenantAccessor accessor)
        {
            accessor.TenantId = tenantId;
        }

        await _next(context);
    }
}
