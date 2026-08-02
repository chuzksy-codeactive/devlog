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
        await ReplaceEmbeddingsAsync(
            publishedEntry.TenantId,
            EmbeddingSourceType.PublishedEntry,
            publishedEntry.Id,
            chunks,
            cancellationToken);
    }

    // Milestone 12 bugfix: Ask AI was answering "no evidence" for questions
    // a candidate's Experience data clearly answered (e.g. "any C#
    // experience?" when C# was right there in a role's Technologies list),
    // because nothing ever embedded Experience rows at all — Ask AI's
    // vector search can only ever see what's actually in the `embeddings`
    // table, and only EmbedPublishedEntryAsync ever wrote to it. Unlike
    // Sprint Journal entries, Experience rows skip the sanitize pipeline
    // entirely and embed directly: CompanyLabel is free text the creator
    // types themselves, already pre-anonymized by design (see Experience.cs
    // — "never a foreign key to a real employer record"), so there's
    // nothing here that needs an LLM sanitization pass first. Called
    // directly from ExperienceController on create/update (and
    // RemoveExperienceEmbeddingsAsync below on delete) rather than from
    // any single "entry point" the way SanitizationPipelineService.ApproveAsync
    // is for published entries — that single-entry-point rule exists
    // specifically to guarantee unsanitized content never gets embedded,
    // which doesn't apply here since there's no sanitization step to skip.
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

    // Called from ExperienceController.Delete — a removed Experience row
    // must not leave orphaned, unreachable-by-id embedding rows behind
    // (same reasoning as the delete-then-insert inside ReplaceEmbeddingsAsync,
    // just with nothing to re-insert).
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

    // A short, natural-language flattening of the structured row — written
    // the way a recruiter's question is actually phrased ("any C#
    // experience?"), not as a label:value dump, so it embeds and retrieves
    // the way the rest of the RAG prompt content does.
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

    // Shared delete-then-insert body for both source types — re-embedding
    // an edited row (published entry or experience) should replace stale
    // chunks wholesale, not leave orphaned rows from a previous version
    // pointing at outdated text.
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
