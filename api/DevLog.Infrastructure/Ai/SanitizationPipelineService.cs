using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Infrastructure.Ai;

public enum SanitizationLimitReason
{
    HourlyRateLimit,
    MonthlyQuota
}

public class SanitizationLimitExceededException : Exception
{
    public SanitizationLimitReason Reason { get; }

    public SanitizationLimitExceededException(SanitizationLimitReason reason, string message) : base(message)
    {
        Reason = reason;
    }
}

public class SanitizationPipelineService
{
    private const int MaxRunsPerTenantPerHour = 10;

    private readonly DevLogDbContext _db;
    private readonly ILlmProvider _llmProvider;
    private readonly EmbeddingPipelineService _embeddingPipeline;
    private readonly PromptInjectionTelemetry _promptInjectionTelemetry;

    public SanitizationPipelineService(
        DevLogDbContext db,
        ILlmProvider llmProvider,
        EmbeddingPipelineService embeddingPipeline,
        PromptInjectionTelemetry promptInjectionTelemetry)
    {
        _db = db;
        _llmProvider = llmProvider;
        _embeddingPipeline = embeddingPipeline;
        _promptInjectionTelemetry = promptInjectionTelemetry;
    }

    public async Task<RawEntry> SanitizeAsync(Guid rawEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.RawEntries.FirstOrDefaultAsync(e => e.Id == rawEntryId, cancellationToken)
            ?? throw new InvalidOperationException($"Raw entry {rawEntryId} not found.");

        if (entry.Status == RawEntryStatus.Approved)
        {
            throw new InvalidOperationException(
                "Cannot re-sanitize an already-approved entry. Editing a published entry is a later milestone (Section 3's raw -> sanitize -> republish flow).");
        }

        var recentRunCount = await _db.SanitizationRunLogs
            .CountAsync(l => l.TenantId == entry.TenantId && l.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentRunCount >= MaxRunsPerTenantPerHour)
        {
            throw new SanitizationLimitExceededException(
                SanitizationLimitReason.HourlyRateLimit,
                "Hourly sanitization limit reached. Try again later.");
        }

        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == entry.TenantId, cancellationToken);
        if (subscription is not null)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyRunCount = await _db.SanitizationRunLogs
                .CountAsync(l => l.TenantId == entry.TenantId && l.CreatedAt >= monthStart, cancellationToken);
            if (monthlyRunCount >= subscription.SanitizationQuota)
            {
                throw new SanitizationLimitExceededException(
                    SanitizationLimitReason.MonthlyQuota,
                    "Monthly sanitization quota reached.");
            }
        }

        entry.Status = RawEntryStatus.Sanitizing;
        await _db.SaveChangesAsync(cancellationToken);

        var blocklist = await _db.Experiences
            .Where(e => e.TenantId == entry.TenantId && e.CompanyLabel != string.Empty)
            .Select(e => e.CompanyLabel)
            .Distinct()
            .ToListAsync(cancellationToken);

        _promptInjectionTelemetry.Scan(
            flow: "sanitization",
            inputType: "raw_entry",
            content: entry.RawContent,
            tenantId: entry.TenantId);

        var result = await _llmProvider.SanitizeAsync(entry.RawContent, blocklist, cancellationToken);

        entry.SanitizedPreview = result.SanitizedContent;
        entry.SanitizationModelVersion = result.ModelVersion;
        entry.Status = RawEntryStatus.PendingApproval;

        _db.SanitizationRunLogs.Add(new SanitizationRunLog
        {
            Id = Guid.NewGuid(),
            TenantId = entry.TenantId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task<PublishedEntry> ApproveAsync(Guid rawEntryId, string approvedBy, CancellationToken cancellationToken = default)
    {
        var entry = await _db.RawEntries
            .Include(e => e.PublishedEntry)
            .FirstOrDefaultAsync(e => e.Id == rawEntryId, cancellationToken)
            ?? throw new InvalidOperationException($"Raw entry {rawEntryId} not found.");

        if (entry.Status != RawEntryStatus.PendingApproval || entry.SanitizedPreview is null)
        {
            throw new InvalidOperationException("Entry must be sanitized (status PendingApproval) before it can be approved.");
        }

        if (entry.PublishedEntry is not null)
        {
            throw new InvalidOperationException("This entry has already been published.");
        }

        var published = new PublishedEntry
        {
            Id = Guid.NewGuid(),
            RawEntryId = entry.Id,
            TenantId = entry.TenantId,
            Title = entry.Title,
            SanitizedContent = entry.SanitizedPreview,
            SourceEntryType = entry.EntryType,
            PublishedAt = DateTime.UtcNow
        };
        _db.PublishedEntries.Add(published);

        var audit = new SanitizationAudit
        {
            Id = Guid.NewGuid(),
            PublishedEntryId = published.Id,
            ModelVersion = entry.SanitizationModelVersion ?? "unknown",
            Diff = $"Raw length: {entry.RawContent.Length} chars -> Sanitized length: {entry.SanitizedPreview.Length} chars.",
            ApprovedBy = approvedBy,
            ApprovedAt = DateTime.UtcNow
        };
        _db.SanitizationAudits.Add(audit);

        entry.Status = RawEntryStatus.Approved;

        await _db.SaveChangesAsync(cancellationToken);

        await _embeddingPipeline.EmbedPublishedEntryAsync(published, cancellationToken);

        return published;
    }
}
