namespace DevLog.Infrastructure.MultiTenancy;

// Populated per-request by TenantResolutionMiddleware (in DevLog.Api) from
// the verified JWT's tenant_id claim — never from a path parameter or
// request body. TenantConnectionInterceptor reads this to set the RLS
// session variable. See architecture doc Section 4's "Key point."
public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; }
}
