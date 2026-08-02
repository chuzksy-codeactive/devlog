using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Infrastructure.Ai;

// Milestone 12: distinguishes the two ways SanitizeAsync can be refused for
// cost-abuse reasons, so EntriesController can return 429 either way but
// still surface which limit was hit.
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

// Orchestrates the flow from architecture doc Section 3:
// raw_entries -> sanitize -> sanitized_draft -> creator approves -> published_entries
// + sanitization_audit. Synchronous for this milestone (no Hangfire job queue
// built yet — see DevLog-MVP-Roadmap.md); the async-job version described in
// the architecture doc's flowchart is a later refinement of the same steps,
// not a different pipeline.
public class SanitizationPipelineService
{
    // Section 9's cost-abuse control, closed in Milestone 12 — mirrors
    // AskAiService's MaxQueriesPerTenantPerHour shape (a row count against a
    // dedicated log table rather than a Redis counter, same simplification
    // as everywhere else pre-Redis). Sanitization is a deliberate creator
    // action behind auth, not a public anonymous endpoint like Ask AI, so
    // the cap is much lower.
    private const int MaxRunsPerTenantPerHour = 10;

    private readonly DevLogDbContext _db;
    private readonly ILlmProvider _llmProvider;
    private readonly EmbeddingPipelineService _embeddingPipeline;

    public SanitizationPipelineService(
        DevLogDbContext db,
        ILlmProvider llmProvider,
        EmbeddingPipelineService embeddingPipeline)
    {
        _db = db;
        _llmProvider = llmProvider;
        _embeddingPipeline = embeddingPipeline;
    }

    public async Task<RawEntry> SanitizeAsync(Guid rawEntryId, CancellationToken cancellationToken = default)
    {
        // No explicit tenant filter here, same pattern as PortfolioController —
        // RLS is what actually stops a cross-tenant id from resolving to a row.
        var entry = await _db.RawEntries.FirstOrDefaultAsync(e => e.Id == rawEntryId, cancellationToken)
            ?? throw new InvalidOperationException($"Raw entry {rawEntryId} not found.");

        if (entry.Status == RawEntryStatus.Approved)
        {
            throw new InvalidOperationException(
                "Cannot re-sanitize an already-approved entry. Editing a published entry is a later milestone (Section 3's raw -> sanitize -> republish flow).");
        }

        // Checked before the LLM call, not after — a rejected request should
        // never incur the cost it's trying to prevent. Logged once the run
        // actually happens, below, not here.
        var recentRunCount = await _db.SanitizationRunLogs
            .CountAsync(l => l.TenantId == entry.TenantId && l.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentRunCount >= MaxRunsPerTenantPerHour)
        {
            throw new SanitizationLimitExceededException(
                SanitizationLimitReason.HourlyRateLimit,
                "Hourly sanitization limit reached. Try again later.");
        }

        // Subscription.SanitizationQuota has existed since early in this
        // build "specifically so a per-tenant rate cap can be enforced"
        // (its own doc comment), but nothing ever counted against it until
        // now. A calendar-month window, not a rolling 30 days — simplest
        // definition that matches "monthly quota" as named.
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

        // Blocklist seed — Section 3, item 1: the creator's own
        // experiences.company_label values, fed to the prompt as known
        // must-strip terms rather than making the model guess at "the employer."
        var blocklist = await _db.Experiences
            .Where(e => e.TenantId == entry.TenantId && e.CompanyLabel != string.Empty)
            .Select(e => e.CompanyLabel)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = await _llmProvider.SanitizeAsync(entry.RawContent, blocklist, cancellationToken);

        entry.SanitizedPreview = result.SanitizedContent;
        entry.SanitizationModelVersion = result.ModelVersion;
        entry.Status = RawEntryStatus.PendingApproval;

        // Logged once the LLM call actually happened — this row is what
        // both checks above count against next time, for this tenant.
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
            // Editing an already-published entry (re-sanitize + re-approve)
            // is Section 3's "raw -> sanitize -> republish flow" — a later
            // milestone. For now, approval is a one-way action per entry.
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

        // The audit row that makes the anonymization pipeline defensible
        // after the fact (Section 3) — model version, a diff summary,
        // approver, and timestamp. Deliberately NOT a full raw-vs-sanitized
        // text diff: storing raw content verbatim here, even in a
        // non-public table, would grow the exact leak surface this pipeline
        // exists to shrink. A length delta is enough for a human reviewing
        // a dispute to sanity-check that sanitization did something real.
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

        // Embedding pipeline triggered right here — Section 3's flowchart:
        // "G --> I[Embedding pipeline triggered]." This is the single entry
        // point (Section 1) that guards against unsanitized content ever
        // being embedded: nothing reaches this line without having just
        // become a published_entries row above.
        await _embeddingPipeline.EmbedPublishedEntryAsync(published, cancellationToken);

        return published;
    }
}
