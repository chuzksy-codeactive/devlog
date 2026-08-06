namespace DevLog.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "LocalDisk";

    public string LocalDiskPath { get; set; } = "uploads";

    public string PublicBaseUrl { get; set; } = "http://localhost:61726/uploads";

    public string? AzureBlobConnectionString { get; set; }
    public string AzureBlobContainerName { get; set; } = "cv-private";

    public int SignedUrlLifetimeMinutes { get; set; } = 10;
}
