using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.MultiTenancy;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

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
        await _embeddingPipeline.EmbedExperienceAsync(experience);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMine), new { }, ToDto(experience));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExperienceRequest request)
    {
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

        await _embeddingPipeline.EmbedExperienceAsync(experience);
        await _db.SaveChangesAsync();

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
