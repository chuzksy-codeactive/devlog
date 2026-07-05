using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Covers Section 3's full flow: create draft -> sanitize -> creator
// approves -> published_entries + sanitization_audit. Every endpoint is
// creator-only (role = creator per Section 4's role table); recruiters
// never touch raw_entries at all.
[ApiController]
[Route("api/entries")]
[Authorize]
public class EntriesController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly SanitizationPipelineService _sanitizationPipeline;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public EntriesController(
        DevLogDbContext db,
        SanitizationPipelineService sanitizationPipeline,
        ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _sanitizationPipeline = sanitizationPipeline;
        _tenantAccessor = tenantAccessor;
    }

    // POST /api/entries — create a raw draft. tenant_id comes from the
    // verified JWT via ICurrentTenantAccessor, never from the request body —
    // Section 4's "never from client input directly" rule.
    [HttpPost]
    public async Task<IActionResult> CreateEntry([FromBody] CreateEntryRequest request)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        if (string.IsNullOrWhiteSpace(request.RawContent))
        {
            return BadRequest("rawContent is required.");
        }

        var entry = new RawEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntryType = request.EntryType,
            RawContent = request.RawContent,
            Status = RawEntryStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.RawEntries.Add(entry);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, ToDto(entry));
    }

    // GET /api/entries/{id} — creator's own view, including the sanitized
    // preview once it exists. No WHERE tenant_id clause — same deliberate
    // pattern as PortfolioController; RLS is what actually enforces scope.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEntry(Guid id)
    {
        var entry = await _db.RawEntries.FirstOrDefaultAsync(e => e.Id == id);
        return entry is null ? NotFound() : Ok(ToDto(entry));
    }

    // POST /api/entries/{id}/sanitize — runs the LLM sanitization pass,
    // stores the result as SanitizedPreview. Synchronous for this milestone
    // (no Hangfire queue yet); safe to call again on a Draft/Rejected entry.
    [HttpPost("{id:guid}/sanitize")]
    public async Task<IActionResult> SanitizeEntry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _sanitizationPipeline.SanitizeAsync(id, cancellationToken);
            return Ok(ToDto(entry));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // POST /api/entries/{id}/approve — creator confirms the sanitized
    // preview is safe to publish. Writes published_entries + sanitization_audit
    // in one transaction (SaveChangesAsync inside ApproveAsync).
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveEntry(Guid id, CancellationToken cancellationToken)
    {
        var approvedBy = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "unknown";

        try
        {
            var published = await _sanitizationPipeline.ApproveAsync(id, approvedBy, cancellationToken);
            return Ok(new
            {
                published.Id,
                published.RawEntryId,
                published.SanitizedContent,
                published.PublishedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static object ToDto(RawEntry entry) => new
    {
        entry.Id,
        entry.EntryType,
        entry.RawContent,
        entry.SanitizedPreview,
        entry.SanitizationModelVersion,
        entry.Status,
        entry.CreatedAt
    };

    public record CreateEntryRequest(RawEntryType EntryType, string RawContent);
}
