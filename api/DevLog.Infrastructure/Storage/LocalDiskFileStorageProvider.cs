using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Storage;

public class LocalDiskFileStorageProvider : IFileStorageProvider
{
    private readonly StorageOptions _options;

    public LocalDiskFileStorageProvider(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var tenantDirectory = Path.Combine(_options.LocalDiskPath, tenantId.ToString());
        Directory.CreateDirectory(tenantDirectory);

        var filePath = Path.Combine(tenantDirectory, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{tenantId}/{fileName}";
    }

    public Task DeleteAsync(
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_options.LocalDiskPath, tenantId.ToString(), fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<string> GetReadUrlAsync(
        string storedFileUrl,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(storedFileUrl);
    }
}
