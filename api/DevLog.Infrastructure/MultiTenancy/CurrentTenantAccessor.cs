namespace DevLog.Infrastructure.MultiTenancy;

// Scoped (per-request) implementation. The setter is used by
// TenantResolutionMiddleware; everything else should only ever read
// ICurrentTenantAccessor.TenantId.
public class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public Guid? TenantId { get; set; }
}
