using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

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
