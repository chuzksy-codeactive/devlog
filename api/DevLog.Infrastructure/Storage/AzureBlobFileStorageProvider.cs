using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Storage;

public class AzureBlobFileStorageProvider : IFileStorageProvider
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobFileStorageProvider(IOptions<StorageOptions> options)
    {
        var storageOptions = options.Value;

        if (string.IsNullOrWhiteSpace(storageOptions.AzureBlobConnectionString))
        {
            throw new InvalidOperationException(
                "Storage:AzureBlobConnectionString must be configured when Storage:Provider is AzureBlob.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.AzureBlobContainerName))
        {
            throw new InvalidOperationException(
                "Storage:AzureBlobContainerName must be configured when Storage:Provider is AzureBlob.");
        }

        _containerClient = new BlobContainerClient(
            storageOptions.AzureBlobConnectionString,
            storageOptions.AzureBlobContainerName);
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"{tenantId}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "application/octet-stream"
        };

        await blobClient.UploadAsync(content, overwrite: true, cancellationToken);
        await blobClient.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var blobName = $"{tenantId}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
