using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Storage;

// Only implementation of IFileStorageProvider for now — matches this
// build's "local only, for now" hosting decision. Files land under
// {LocalDiskPath}/{tenantId}/{fileName}; Program.cs serves that same
// directory as static files at {PublicBaseUrl}'s path, so the URL this
// returns is directly fetchable, not just a filesystem path.
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

        // Overwrite, not append/version — see IFileStorageProvider's doc
        // comment. FileMode.Create truncates an existing file at this path
        // rather than leaving stale bytes past the new (possibly shorter)
        // content's length.
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
}
