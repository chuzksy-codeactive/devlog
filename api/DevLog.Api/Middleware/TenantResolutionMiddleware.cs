using DevLog.Infrastructure.MultiTenancy;

namespace DevLog.Api.Middleware;

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
