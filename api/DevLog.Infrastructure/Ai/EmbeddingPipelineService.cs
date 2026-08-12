using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace DevLog.Infrastructure.Ai;

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
        await ReplaceEmbeddingsAsync(
            publishedEntry.TenantId,
            EmbeddingSourceType.PublishedEntry,
            publishedEntry.Id,
            chunks,
            cancellationToken);
    }

    public async Task EmbedExperienceAsync(Experience experience, CancellationToken cancellationToken = default)
    {
        var text = BuildExperienceText(experience);
        var chunks = TextChunker.Chunk(text);
        await ReplaceEmbeddingsAsync(
            experience.TenantId,
            EmbeddingSourceType.Experience,
            experience.Id,
            chunks,
            cancellationToken);
    }

    public async Task RemoveExperienceEmbeddingsAsync(Guid experienceId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Embeddings
            .Where(e => e.SourceType == EmbeddingSourceType.Experience && e.SourceId == experienceId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _db.Embeddings.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemovePublishedEntryEmbeddingsAsync(Guid publishedEntryId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Embeddings
            .Where(e => e.SourceType == EmbeddingSourceType.PublishedEntry && e.SourceId == publishedEntryId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _db.Embeddings.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildExperienceText(Experience experience)
    {
        var period = experience.EndDate is { } end
            ? $"{experience.StartDate:yyyy-MM} to {end:yyyy-MM}"
            : $"{experience.StartDate:yyyy-MM} to present";

        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(experience.Title)
                ? $"{experience.CompanyLabel} ({period})"
                : $"{experience.Title} at {experience.CompanyLabel} ({period})"
        };

        if (experience.Technologies.Count > 0)
        {
            lines.Add($"Technologies used: {string.Join(", ", experience.Technologies)}.");
        }

        if (!string.IsNullOrWhiteSpace(experience.KeyAchievement))
        {
            lines.Add(experience.KeyAchievement);
        }

        return string.Join("\n\n", lines);
    }

    private async Task ReplaceEmbeddingsAsync(
        Guid tenantId,
        EmbeddingSourceType sourceType,
        Guid sourceId,
        IReadOnlyList<string> chunks,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Embeddings
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
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
                TenantId = tenantId,
                SourceType = sourceType,
                SourceId = sourceId,
                ChunkIndex = i,
                ChunkText = chunks[i],
                Vector = new Vector(vectors[i]),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
