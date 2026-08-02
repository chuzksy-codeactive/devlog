namespace DevLog.Infrastructure.Storage;

// Milestone 13b — the same vendor-abstraction shape as ILlmProvider,
// IEmbeddingProvider, and IEmailProvider: one interface, a swappable
// implementation behind it. LocalDiskFileStorageProvider is the only
// implementation for now (matches this project's "local only, for now"
// hosting decision) — a future Azure Blob Storage implementation slots in
// behind this interface without touching anything that calls it, same
// story as every other provider swap already made in this codebase.
public interface IFileStorageProvider
{
    // Returns a URL the stored file can be fetched from. Overwrites
    // whatever was previously stored at the same (tenantId, fileName) pair
    // — a resume upload replaces the last one, it doesn't accumulate.
    Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}
