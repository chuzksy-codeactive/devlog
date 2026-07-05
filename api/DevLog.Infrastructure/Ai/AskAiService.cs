using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DevLog.Infrastructure.Ai;

public enum AskAiOutcome
{
    Answered,
    PortfolioNotFound,
    RateLimited
}

public record AskAiResult(AskAiOutcome Outcome, string? Answer = null);

// The retrieval + generation half of Section 2's RAG pipeline — "embed
// question -> cosine similarity search WHERE tenant_id = candidate ->
// generate answer." Deliberately anonymous: the MVP roadmap specifies
// "anonymous browsing... single-candidate Ask AI" for Phase 1, so there's
// no recruiter identity to check here, only the target portfolio's
// visibility and a coarse per-tenant rate cap.
public class AskAiService
{
    private const int TopK = 5;

    // A hard, simple per-tenant hourly cap — the cost-abuse control Section
    // 9 flagged, implemented as a row count against ai_query_logs rather
    // than a Redis counter (Redis isn't wired up yet; same simplification
    // pattern as the rest of this milestone). Revisit with Redis once real
    // traffic makes a DB count-per-request too expensive.
    private const int MaxQueriesPerTenantPerHour = 30;

    private readonly DevLogDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILlmProvider _llmProvider;

    public AskAiService(DevLogDbContext db, IEmbeddingProvider embeddingProvider, ILlmProvider llmProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _llmProvider = llmProvider;
    }

    public async Task<AskAiResult> AskAsync(
        string slug,
        string question,
        string? askerHash,
        CancellationToken cancellationToken = default)
    {
        // No tenant session context is set for this request (anonymous —
        // see class doc comment), so this read only returns a row at all
        // because of the public-read RLS policy on portfolios/tenants, not
        // the creator-scoped one. Visibility is checked again here in the
        // app layer too — defense-in-depth, same principle as everywhere
        // else in this schema.
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return new AskAiResult(AskAiOutcome.PortfolioNotFound);
        }

        var tenantId = portfolio.TenantId;

        var recentQueryCount = await _db.AiQueryLogs
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentQueryCount >= MaxQueriesPerTenantPerHour)
        {
            return new AskAiResult(AskAiOutcome.RateLimited);
        }

        var questionEmbedding = await _embeddingProvider.EmbedAsync(question, cancellationToken);
        var questionVector = new Vector(questionEmbedding);

        // Cosine distance (smaller = more similar) via Pgvector.EntityFrameworkCore's
        // LINQ translation of the `<=>` operator — runs as a real SQL ORDER BY,
        // not an in-memory scan.
        var topChunks = await _db.Embeddings
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Vector.CosineDistance(questionVector))
            .Take(TopK)
            .Select(e => e.ChunkText)
            .ToListAsync(cancellationToken);

        var answer = await _llmProvider.AnswerQuestionAsync(question, topChunks, cancellationToken);

        _db.AiQueryLogs.Add(new AiQueryLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Question = question,
            AskerHash = askerHash,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new AskAiResult(AskAiOutcome.Answered, answer);
    }
}
