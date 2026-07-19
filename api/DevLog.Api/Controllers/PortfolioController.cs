using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

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
        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == tenantId);

        if (portfolio is null)
        {
            return NotFound();
        }

        return Ok(portfolio);
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

    public record UpdatePortfolioRequest(
        string? Headline,
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
