using Pgvector;

namespace DevLog.Domain.Entities;

public class Embedding
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public EmbeddingSourceType SourceType { get; set; } = EmbeddingSourceType.PublishedEntry;
    public Guid SourceId { get; set; } // PublishedEntry.Id, for now (see EmbeddingSourceType)

    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;

    public Vector Vector { get; set; } = new Vector(new float[1536]);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
