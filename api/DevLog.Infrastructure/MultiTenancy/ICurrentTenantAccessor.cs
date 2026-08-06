namespace DevLog.Infrastructure.MultiTenancy;

public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; }
}
