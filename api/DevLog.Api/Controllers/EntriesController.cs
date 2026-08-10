using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.MultiTenancy;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> CreateEntry([FromBody] CreateEntryRequest request)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("title is required.");
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
            Title = request.Title,
            RawContent = request.RawContent,
            Status = RawEntryStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.RawEntries.Add(entry);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, ToDto(entry));
    }

    [HttpGet]
    public async Task<IActionResult> ListMine()
    {
        var entries = await _db.RawEntries
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return Ok(entries.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEntry(Guid id)
    {
        var entry = await _db.RawEntries.FirstOrDefaultAsync(e => e.Id == id);
        return entry is null ? NotFound() : Ok(ToDto(entry));
    }

    [HttpPost("{id:guid}/sanitize")]
    [EnableRateLimiting("SanitizeEntry")]
    public async Task<IActionResult> SanitizeEntry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _sanitizationPipeline.SanitizeAsync(id, cancellationToken);
            return Ok(ToDto(entry));
        }
        catch (SanitizationLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

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
        entry.Title,
        entry.RawContent,
        entry.SanitizedPreview,
        entry.SanitizationModelVersion,
        entry.Status,
        entry.CreatedAt
    };

    public record CreateEntryRequest(
        RawEntryType EntryType,
        [param: Required, MaxLength(200)] string Title,
        [param: Required, MaxLength(20000)] string RawContent);
}
