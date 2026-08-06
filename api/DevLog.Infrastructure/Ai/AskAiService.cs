using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public class AskAiService
{
    private const int TopK = 5;

    private const int MaxQueriesPerTenantPerHour = 30;

    private readonly DevLogDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILlmProvider _llmProvider;
    private readonly PromptInjectionTelemetry _promptInjectionTelemetry;
    private readonly ILogger<AskAiService> _logger;

    public AskAiService(
        DevLogDbContext db,
        IEmbeddingProvider embeddingProvider,
        ILlmProvider llmProvider,
        PromptInjectionTelemetry promptInjectionTelemetry,
        ILogger<AskAiService> logger)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _llmProvider = llmProvider;
        _promptInjectionTelemetry = promptInjectionTelemetry;
        _logger = logger;
    }

    public async Task<AskAiResult> AskAsync(
        string slug,
        string question,
        string? askerHash,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return new AskAiResult(AskAiOutcome.PortfolioNotFound);
        }

        _promptInjectionTelemetry.Scan(
            flow: "ask_ai",
            inputType: "question",
            content: question,
            tenantId: portfolio.TenantId,
            slug: slug);

        var tenantId = portfolio.TenantId;

        var recentQueryCount = await _db.AiQueryLogs
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentQueryCount >= MaxQueriesPerTenantPerHour)
        {
            return new AskAiResult(AskAiOutcome.RateLimited);
        }

        var questionEmbedding = await _embeddingProvider.EmbedAsync(question, cancellationToken);
        var questionVector = new Vector(questionEmbedding);

        var topChunks = await _db.Embeddings
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Vector.CosineDistance(questionVector))
            .Take(TopK)
            .Select(e => e.ChunkText)
            .ToListAsync(cancellationToken);

        _promptInjectionTelemetry.Scan(
            flow: "ask_ai",
            inputType: "retrieved_context",
            content: string.Join("\n", topChunks),
            tenantId: tenantId,
            slug: slug);

        var answer = await _llmProvider.AnswerQuestionAsync(question, topChunks, cancellationToken);

        try
        {
            _db.AiQueryLogs.Add(new AiQueryLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Question = question,
                AskerHash = askerHash,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI query for tenant {TenantId}", tenantId);
        }

        return new AskAiResult(AskAiOutcome.Answered, answer);
    }
}
