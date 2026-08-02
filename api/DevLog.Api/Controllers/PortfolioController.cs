using System.Security.Cryptography;
using System.Text;
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
    private readonly ILogger<PortfolioController> _logger;

    public PortfolioController(DevLogDbContext db, ICurrentTenantAccessor tenantAccessor, ILogger<PortfolioController> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
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
        return Ok(ToMyPortfolioDto(portfolio));
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

        // .Include(p => p.Tenant) here too, for the same reason as
        // GetMyPortfolio — the shaped DTO below needs Slug regardless, and
        // relying on TenantResolutionMiddleware's own earlier Tenant lookup
        // to have incidentally fixed up this navigation via EF's change
        // tracker (which is what caused this endpoint's original bug: it
        // returned the raw entity, and that same incidental fixup turned
        // Tenant<->Portfolio into a live reference cycle that
        // System.Text.Json then choked on) isn't something to depend on.
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);
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

        return Ok(ToMyPortfolioDto(portfolio));
    }

    // Shared by GetMyPortfolio/UpdateMyPortfolio — the creator-authenticated
    // shape (adds Slug, Visibility, Discoverable, NotificationEmail on top
    // of what GetPublicPortfolio exposes). Never return a Portfolio entity
    // directly from an action: Tenant<->Portfolio is a circular EF
    // navigation, and the moment anything else in the same request scope
    // (TenantResolutionMiddleware's own Tenant lookup runs on every
    // authenticated request) has already tracked the matching Tenant row,
    // EF's change-tracker fixup silently wires the two together — System.Text.Json
    // then throws "a possible object cycle was detected" trying to
    // serialize it, regardless of whether this query itself included Tenant.
    private static object ToMyPortfolioDto(Portfolio portfolio) => new
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
    };

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

        // Milestone 12 — the portfolio_view_events half of the roadmap's
        // "instrument everything... populated from day one" item. Logged
        // here rather than via Next.js middleware/analytics, so a view
        // counts once per real backend load regardless of frontend caching
        // behavior. Best-effort: a logging failure must never break the
        // actual page load, same "the real work already succeeded" posture
        // as InterviewRequestService's notification email. Unlike that
        // email send, though, a failure here gets logged, not silently
        // swallowed — sql/008 fixed the one way this was actually failing
        // (a missing public_insert RLS policy for the anonymous caller),
        // found only because it failed with zero visibility the first time.
        try
        {
            _db.PortfolioViewEvents.Add(new PortfolioViewEvent
            {
                Id = Guid.NewGuid(),
                TenantId = portfolio.TenantId,
                ViewerHash = HashRemoteIp(HttpContext.Connection.RemoteIpAddress?.ToString()),
                ViewedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log portfolio view event for tenant {TenantId}", portfolio.TenantId);
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

    // Same construction as AskAiController.HashRemoteIp — a coarse,
    // non-identifying signal for portfolio_view_events, not a raw IP.
    private static string? HashRemoteIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes);
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
