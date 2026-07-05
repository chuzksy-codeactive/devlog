using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace DevLog.Infrastructure.Ai;

// The embedding half of Section 2's RAG pipeline. Triggered from exactly
// one place — SanitizationPipelineService.ApproveAsync — per Section 1's
// cross-cutting decision that "the embedding pipeline has exactly one
// entry point," which is what keeps unsanitized content from ever being
// embedded. Synchronous for this milestone, same simplification already
// made for sanitization (no Hangfire queue yet).
public class EmbeddingPipelineService
{
    private readonly DevLogDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;

    public EmbeddingPipelineService(DevLogDbContext db, IEmbeddingProvider embeddingProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
    }

    public async Task EmbedPublishedEntryAsync(PublishedEntry publishedEntry, CancellationToken cancellationToken = default)
    {
        var chunks = TextChunker.Chunk(publishedEntry.SanitizedContent);

        // Delete-then-insert rather than diffing — re-embedding an edited
        // entry (the "raw -> sanitize -> republish" flow, not built yet)
        // should replace stale chunks wholesale, not leave orphaned rows
        // from a previous version pointing at outdated text.
        var existing = await _db.Embeddings
            .Where(e => e.SourceType == EmbeddingSourceType.PublishedEntry && e.SourceId == publishedEntry.Id)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _db.Embeddings.RemoveRange(existing);
        }

        if (chunks.Count == 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var vectors = await _embeddingProvider.EmbedBatchAsync(chunks, cancellationToken);

        for (var i = 0; i < chunks.Count; i++)
        {
            _db.Embeddings.Add(new Embedding
            {
                Id = Guid.NewGuid(),
                TenantId = publishedEntry.TenantId,
                SourceType = EmbeddingSourceType.PublishedEntry,
                SourceId = publishedEntry.Id,
                ChunkIndex = i,
                ChunkText = chunks[i],
                Vector = new Vector(vectors[i]),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
