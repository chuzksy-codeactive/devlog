using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/portfolio/{slug}/entries")]
public class SprintJournalController : ControllerBase
{
    private readonly DevLogDbContext _db;

    public SprintJournalController(DevLogDbContext db)
    {
        _db = db;
    }

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
