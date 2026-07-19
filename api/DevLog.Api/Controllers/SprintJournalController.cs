using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Deliberately anonymous — no [Authorize] — same reasoning as
// AskAiController/InterviewRequestsController: "anonymous browsing of
// published portfolios" is the Phase 1 scope, no recruiter login exists.
// Filters to SourceEntryType == SprintEntry only, since published_entries
// also holds Experience-sourced rows that flow through the same
// sanitize-and-approve pipeline (Section 3's "applies uniformly" note) —
// those belong to the Experience page, not here.
[ApiController]
[Route("api/portfolio/{slug}/entries")]
public class SprintJournalController : ControllerBase
{
    private readonly DevLogDbContext _db;

    public SprintJournalController(DevLogDbContext db)
    {
        _db = db;
    }

    // GET /api/portfolio/{slug}/entries — listing. Deliberately excludes
    // SanitizedContent (the full case-study body) to keep the list payload
    // light; the detail endpoint below is where that's fetched.
    [HttpGet]
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
            .Where(e => e.TenantId == portfolio.TenantId && e.SourceEntryType == RawEntryType.SprintEntry)
            .OrderByDescending(e => e.PublishedAt)
            .Select(e => new { e.Id, e.Title, e.Tags, e.PublishedAt })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    // GET /api/portfolio/{slug}/entries/{id} — detail, includes the full
    // sanitized markdown body. The frontend is responsible for rendering it
    // through a restricted markdown renderer with no raw HTML passthrough
    // (SanitizedMarkdown.tsx) — this endpoint just returns the stored text
    // as-is, same "sanitization happened once, at write time" principle as
    // everywhere else in this pipeline.
    [HttpGet("{id:guid}")]
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
            .Where(e => e.Id == id && e.TenantId == portfolio.TenantId && e.SourceEntryType == RawEntryType.SprintEntry)
            .Select(e => new { e.Id, e.Title, e.SanitizedContent, e.Tags, e.PublishedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        return Ok(entry);
    }
}
