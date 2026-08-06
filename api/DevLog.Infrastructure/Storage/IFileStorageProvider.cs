namespace DevLog.Infrastructure.Storage;

public interface IFileStorageProvider
{
    Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(
        string storedFileUrl,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);
}
