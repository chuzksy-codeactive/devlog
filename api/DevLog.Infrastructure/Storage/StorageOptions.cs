namespace DevLog.Infrastructure.Storage;

// Bound from configuration section "Storage".
public class StorageOptions
{
    public const string SectionName = "Storage";

    // Selects which IFileStorageProvider implementation is used.
    // Supported values: "LocalDisk" (default) and "AzureBlob".
    public string Provider { get; set; } = "LocalDisk";

    // Relative or absolute path on disk where uploaded files are written.
    // Relative paths resolve against the API project's working directory —
    // fine for "local only, for now" (this build's actual hosting
    // decision); a production deployment would point this at a mounted
    // volume, or swap IFileStorageProvider's implementation entirely for
    // Azure Blob Storage rather than trying to make local disk work at
    // scale.
    public string LocalDiskPath { get; set; } = "uploads";

    // The origin static files get served from — must match wherever
    // Program.cs's UseStaticFiles is actually mounted. Kept as its own
    // config value rather than reusing Clerk:AuthorizedParty or similar,
    // since this is the API's own origin, not the frontend's.
    public string PublicBaseUrl { get; set; } = "http://localhost:61726/uploads";

    // Azure Blob Storage settings (used when Provider = AzureBlob).
    public string? AzureBlobConnectionString { get; set; }
    public string AzureBlobContainerName { get; set; } = "cv-private";
}
