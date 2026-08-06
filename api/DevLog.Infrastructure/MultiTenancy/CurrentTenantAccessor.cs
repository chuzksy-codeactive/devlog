namespace DevLog.Infrastructure.MultiTenancy;

public class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public Guid? TenantId { get; set; }
}
