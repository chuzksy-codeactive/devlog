using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Milestone 17 — Learning Lab, the 5th and last Product Brief IA surface to
// ship ("monthly diary + career retrospectives, unified as one growth log").
// Public list/detail actions mirror SprintJournalController exactly (same
// portfolio-by-slug + visibility check, same listing/detail split, same
// filter-by-SourceEntryType shape) — the only difference is RawEntryType.LearningLog
// instead of .SprintEntry. Entries already flow through the exact same
// raw_entries -> sanitize -> approve -> published_entries pipeline; the
// dashboard journal composer has offered "Learning Log entry" as an entry
// type since Milestone 11, it just had nowhere public to land until now.
//
// Class-level [Authorize] (same pattern as PortfolioController) because this
// controller also carries the creator-only retention-nudge endpoint below —
// the two public actions override it with [AllowAnonymous].
[ApiController]
[Route("api/portfolio/{slug}/learning")]
[Authorize]
public class LearningLabController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public LearningLabController(DevLogDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    // GET /api/portfolio/{slug}/learning — listing, anonymous.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ListEntries(string slug, CancellationToken cancellationToken)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return NotFound("No public portfolio found at this slug.");
        }

        var entries = await _db.PublishedEntries
            .Where(e => e.TenantId == portfolio.TenantId && e.SourceEntryType == RawEntryType.LearningLog)
            .OrderByDescending(e => e.PublishedAt)
            .Select(e => new { e.Id, e.Title, e.Tags, e.PublishedAt })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    // GET /api/portfolio/{slug}/learning/{id} — detail, anonymous.
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEntry(string slug, Guid id, CancellationToken cancellationToken)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return NotFound("No public portfolio found at this slug.");
        }

        var entry = await _db.PublishedEntries
            .Where(e => e.Id == id && e.TenantId == portfolio.TenantId && e.SourceEntryType == RawEntryType.LearningLog)
            .Select(e => new { e.Id, e.Title, e.SanitizedContent, e.Tags, e.PublishedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        return Ok(entry);
    }

    // GET /api/learning-lab/reminder — the retention nudge (Product Brief
    // gap-closure feature #7: "a monthly diary only works if people keep
    // writing it"). The architecture doc's original design was a Hangfire
    // recurring job that emails a reminder — this codebase has never built
    // a job queue (every LLM/email call so far is deliberately synchronous),
    // and standing one up for a once-a-month email was judged not worth it
    // yet. Decided with Chika via AskUserQuestion: computed live instead,
    // read by the dashboard on every load and rendered as an in-app banner
    // when due — no new infrastructure, works today, and arguably more
    // effective than an easily-ignored email for a single/small-team
    // instance. NudgeActive is true if no Learning Log entry has ever been
    // published, or if the most recent one is more than 30 days old.
    [HttpGet("/api/learning-lab/reminder")]
    public async Task<IActionResult> GetReminderStatus(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var lastPublishedAt = await _db.PublishedEntries
            .Where(e => e.TenantId == tenantId && e.SourceEntryType == RawEntryType.LearningLog)
            .OrderByDescending(e => e.PublishedAt)
            .Select(e => (DateTime?)e.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        int? daysSinceLastEntry = lastPublishedAt is DateTime last
            ? (int)(DateTime.UtcNow - last).TotalDays
            : null;

        var nudgeActive = lastPublishedAt is null || daysSinceLastEntry > 30;

        return Ok(new
        {
            lastPublishedAt,
            daysSinceLastEntry,
            nudgeActive
        });
    }
}
