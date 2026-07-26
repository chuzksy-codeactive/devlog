using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Note: [Authorize] is class-level, but GetPublicPortfolio below overrides
// it with [AllowAnonymous] — same anonymous-by-slug pattern as
// AskAiService/InterviewRequestService/SprintJournalController, just kept
// in this controller rather than a new one since it's still "portfolio"
// data, one route segment over from /me. ASP.NET Core's routing resolves
// the literal "/api/portfolio/me" ahead of the parameterized
// "/api/portfolio/{slug}" automatically, so there's no ambiguity between
// the two actions below.

[ApiController]
[Route("api/portfolio")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PortfolioController(DevLogDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPortfolio()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        // An explicit filter is required here now, not just defense in
        // depth — sql/004 gave portfolios a public_read RLS policy (Ask AI
        // and Request Interview both need to look up a portfolio by slug
        // anonymously), so more than one row is legitimately visible to an
        // authenticated caller: their own, plus any other tenant's public
        // portfolio. "No WHERE clause, RLS enforces scope" only holds for
        // tables where a caller can never see anyone else's row at all
        // (raw_entries, entries via EntriesController) — that invariant
        // doesn't hold here, so FirstOrDefaultAsync() with no filter could
        // return a different tenant's public portfolio instead of the
        // caller's own.
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);

        if (portfolio is null)
        {
            return NotFound();
        }

        // Shaped rather than returning the raw entity — mainly to include
        // Slug (lives on Tenant, not Portfolio; the dashboard needs it to
        // link out to the public portfolio, which nothing before Milestone
        // 11 ever needed). Also sidesteps Tenant<->Portfolio being a
        // circular EF navigation, which System.Text.Json would otherwise
        // choke on now that Tenant is actually included.
        return Ok(new
        {
            slug = portfolio.Tenant!.Slug,
            portfolio.Headline,
            portfolio.DisplayName,
            portfolio.PositioningLine,
            portfolio.PhotoUrl,
            portfolio.CurrentRole,
            portfolio.YearsExperience,
            portfolio.CoreTechnologies,
            portfolio.LinkedInUrl,
            portfolio.GitHubUrl,
            portfolio.CvFileUrl,
            portfolio.Visibility,
            portfolio.Discoverable,
            portfolio.NotificationEmail
        });
    }

    // PUT /api/portfolio/me — closes the "no update endpoint exists yet"
    // gap flagged since Milestone 5/6 (NotificationEmail, and everything
    // else on Portfolio, had to be set via SQL until now). Deliberately a
    // partial update: only fields present (non-null) in the request
    // overwrite the entity, so a creator can change one field without
    // resending everything. Slug lives on Tenant, not Portfolio, and isn't
    // editable here — TenantProvisioningService's placeholder slug is
    // permanent until a dedicated rename endpoint exists (flagged there,
    // not fixed here).
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyPortfolio([FromBody] UpdatePortfolioRequest request)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == tenantId);
        if (portfolio is null)
        {
            return NotFound();
        }

        if (request.Headline is not null) portfolio.Headline = request.Headline;
        if (request.DisplayName is not null) portfolio.DisplayName = request.DisplayName;
        if (request.PositioningLine is not null) portfolio.PositioningLine = request.PositioningLine;
        if (request.PhotoUrl is not null) portfolio.PhotoUrl = request.PhotoUrl;
        if (request.CurrentRole is not null) portfolio.CurrentRole = request.CurrentRole;
        if (request.YearsExperience is not null) portfolio.YearsExperience = request.YearsExperience;
        if (request.CoreTechnologies is not null) portfolio.CoreTechnologies = request.CoreTechnologies;
        if (request.LinkedInUrl is not null) portfolio.LinkedInUrl = request.LinkedInUrl;
        if (request.GitHubUrl is not null) portfolio.GitHubUrl = request.GitHubUrl;
        if (request.CvFileUrl is not null) portfolio.CvFileUrl = request.CvFileUrl;
        if (request.Visibility is not null) portfolio.Visibility = request.Visibility.Value;
        if (request.Discoverable is not null) portfolio.Discoverable = request.Discoverable.Value;
        if (request.NotificationEmail is not null) portfolio.NotificationEmail = request.NotificationEmail;

        await _db.SaveChangesAsync();

        return Ok(portfolio);
    }

    // GET /api/portfolio/{slug} — anonymous, public-facing. Replaces
    // web/src/lib/mock-portfolio.ts, which every public page (Home,
    // Experience) has run on exclusively since Milestone 3; nothing before
    // this endpoint ever actually served real portfolio data. Same
    // portfolio-by-slug + visibility-check pattern as
    // AskAiService/InterviewRequestService/SprintJournalController.
    // Includes experiences inline (a single response, matching the shape
    // the frontend's PortfolioData type already expects) rather than
    // making the client issue a second call to ExperienceController, which
    // is creator-authenticated only anyway and not meant for public reads.
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicPortfolio(string slug)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return NotFound();
        }

        var experiences = await _db.Experiences
            .Where(e => e.TenantId == portfolio.TenantId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        return Ok(new
        {
            slug,
            displayName = portfolio.DisplayName,
            positioningLine = portfolio.PositioningLine,
            currentRoleTitle = portfolio.CurrentRole,
            coreTechnologies = portfolio.CoreTechnologies,
            githubUrl = portfolio.GitHubUrl,
            cvUrl = portfolio.CvFileUrl,
            photoUrl = portfolio.PhotoUrl,
            experiences = experiences.Select(e => new
            {
                id = e.Id,
                experienceType = e.ExperienceType,
                companyLabel = e.CompanyLabel,
                title = e.Title,
                startDate = e.StartDate,
                endDate = e.EndDate,
                technologies = e.Technologies,
                keyAchievement = e.KeyAchievement
            })
        });
    }

    public record UpdatePortfolioRequest(
        string? Headline,
        string? DisplayName,
        string? PositioningLine,
        string? PhotoUrl,
        string? CurrentRole,
        string? YearsExperience,
        List<string>? CoreTechnologies,
        string? LinkedInUrl,
        string? GitHubUrl,
        string? CvFileUrl,
        PortfolioVisibility? Visibility,
        bool? Discoverable,
        string? NotificationEmail);
}
