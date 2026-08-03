using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.MultiTenancy;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Milestone 11 — the creator-facing half of the Experience surface.
// Experience rows could only ever be written via direct SQL/EF before this;
// no API endpoint touched this table at all. sql/006 added RLS
// (tenant_isolation + public_read) specifically so this controller could
// exist safely. Same tenant-scoping pattern as EntriesController: tenantId
// comes from ICurrentTenantAccessor on create, never from the request body;
// reads/updates/deletes carry no explicit WHERE tenant_id clause because
// RLS is what actually enforces scope.
[ApiController]
[Route("api/experiences")]
[Authorize]
public class ExperienceController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly EmbeddingPipelineService _embeddingPipeline;

    public ExperienceController(
        DevLogDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        EmbeddingPipelineService embeddingPipeline)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _embeddingPipeline = embeddingPipeline;
    }

    // GET /api/experiences/me — the creator's own experiences, all of them
    // (including ones a Paused portfolio would otherwise hide from the
    // public endpoint). "me" rather than a bare GET to stay unambiguous
    // next to a possible future GET /api/experiences/{id}.
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var experiences = await _db.Experiences
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        return Ok(experiences.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExperienceRequest request)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        if (string.IsNullOrWhiteSpace(request.CompanyLabel))
        {
            return BadRequest("companyLabel is required.");
        }

        var experience = new Experience
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExperienceType = request.ExperienceType,
            CompanyLabel = request.CompanyLabel.Trim(),
            Title = request.Title?.Trim() ?? string.Empty,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Technologies = request.Technologies ?? new List<string>(),
            KeyAchievement = request.KeyAchievement?.Trim() ?? string.Empty
        };

        _db.Experiences.Add(experience);
        await _db.SaveChangesAsync();

        // Milestone 12 bugfix — see EmbeddingPipelineService.EmbedExperienceAsync's
        // doc comment. Not inside the same SaveChangesAsync/transaction:
        // EmbedBatchAsync is an external OpenAI call, and the Experience
        // row itself must exist as soon as it's saved regardless of
        // whether that call succeeds.
        await _embeddingPipeline.EmbedExperienceAsync(experience);

        return CreatedAtAction(nameof(GetMine), new { }, ToDto(experience));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExperienceRequest request)
    {
        // No WHERE tenant_id clause — RLS scopes this to the caller's own
        // tenant, same as AcceptAsync/DeclineAsync in InterviewRequestService.
        var experience = await _db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
        if (experience is null)
        {
            return NotFound();
        }

        experience.ExperienceType = request.ExperienceType;
        experience.CompanyLabel = request.CompanyLabel.Trim();
        experience.Title = request.Title?.Trim() ?? string.Empty;
        experience.StartDate = request.StartDate;
        experience.EndDate = request.EndDate;
        experience.Technologies = request.Technologies ?? new List<string>();
        experience.KeyAchievement = request.KeyAchievement?.Trim() ?? string.Empty;

        await _db.SaveChangesAsync();

        // Re-embeds from scratch (delete-then-insert inside
        // EmbeddingPipelineService) so an edit's stale chunks never linger
        // — same reasoning as re-approving an edited published entry.
        await _embeddingPipeline.EmbedExperienceAsync(experience);

        return Ok(ToDto(experience));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var experience = await _db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
        if (experience is null)
        {
            return NotFound();
        }

        _db.Experiences.Remove(experience);
        await _db.SaveChangesAsync();

        // Without this, a deleted Experience's chunks stay retrievable by
        // Ask AI forever — the row is gone but its embeddings aren't.
        await _embeddingPipeline.RemoveExperienceEmbeddingsAsync(id);

        return NoContent();
    }

    private static object ToDto(Experience e) => new
    {
        e.Id,
        e.ExperienceType,
        e.CompanyLabel,
        e.Title,
        e.StartDate,
        e.EndDate,
        e.Technologies,
        e.KeyAchievement
    };

    public record ExperienceRequest(
        ExperienceType ExperienceType,
        [param: Required, MaxLength(160)] string CompanyLabel,
        [param: MaxLength(160)] string? Title,
        DateOnly StartDate,
        DateOnly? EndDate,
        [param: MaxLength(30)] List<string>? Technologies,
        [param: MaxLength(2000)] string? KeyAchievement);
}
