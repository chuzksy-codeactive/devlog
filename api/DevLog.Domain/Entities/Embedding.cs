using Pgvector;

namespace DevLog.Domain.Entities;

// The RAG retrieval index — architecture doc Section 2. One row per chunk,
// not per entry, so long entries retrieve at paragraph granularity instead
// of forcing the whole entry into every prompt. Populated exactly once,
// from EmbeddingPipelineService, triggered inside
// SanitizationPipelineService.ApproveAsync — "the embedding pipeline has
// exactly one entry point" (Section 1's cross-cutting decision), which is
// what keeps unsanitized content from ever being embedded.
public class Embedding
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public EmbeddingSourceType SourceType { get; set; } = EmbeddingSourceType.PublishedEntry;
    public Guid SourceId { get; set; } // PublishedEntry.Id, for now (see EmbeddingSourceType)

    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;

    // 1536 dimensions — text-embedding-3-small (OpenAI), the embedding
    // model chosen for this milestone (2026-07-05). If the embedding model
    // ever changes, every existing row needs re-embedding — dimension and
    // model are coupled, which is why re-embedding is a full delete +
    // re-insert per source in EmbeddingPipelineService, not a partial update.
    public Vector Vector { get; set; } = new Vector(new float[1536]);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
