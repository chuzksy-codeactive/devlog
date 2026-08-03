using DevLog.Domain.Entities;
using DevLog.Api.Validation;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Identity;
using DevLog.Infrastructure.MultiTenancy;
using DevLog.Infrastructure.Security;
using DevLog.Infrastructure.Storage;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    private readonly ClerkUserService _clerkUserService;
    private readonly ILogger<PortfolioController> _logger;
    private readonly IIpHashingService _ipHashingService;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly StorageOptions _storageOptions;

    public PortfolioController(
        DevLogDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ClerkUserService clerkUserService,
        ILogger<PortfolioController> logger,
        IIpHashingService ipHashingService,
        IFileStorageProvider fileStorageProvider,
        IOptions<StorageOptions> storageOptions)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _clerkUserService = clerkUserService;
        _logger = logger;
        _ipHashingService = ipHashingService;
        _fileStorageProvider = fileStorageProvider;
        _storageOptions = storageOptions.Value;
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

    [HttpGet("me/cv-access-url")]
    public async Task<IActionResult> GetMyCvAccessUrl(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (portfolio is null || string.IsNullOrWhiteSpace(portfolio.CvFileUrl))
        {
            return NotFound("No CV is currently available for this portfolio.");
        }

        var minutes = Math.Clamp(_storageOptions.SignedUrlLifetimeMinutes, 1, 60);
        var lifetime = TimeSpan.FromMinutes(minutes);
        var readUrl = await _fileStorageProvider.GetReadUrlAsync(portfolio.CvFileUrl, lifetime, cancellationToken);

        _logger.LogInformation(
            "Issued CV access URL for tenant {TenantId} with lifetime {LifetimeSeconds}s.",
            tenantId,
            (int)lifetime.TotalSeconds);

        return Ok(new
        {
            cvAccessUrl = readUrl,
            expiresInSeconds = (int)lifetime.TotalSeconds
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

        string? photoUrl = null;
        if (request.PhotoUrl is not null && !UrlAllowlist.TryNormalizeHttpUrl(request.PhotoUrl, out photoUrl))
        {
            return BadRequest("photoUrl must be an absolute http/https URL.");
        }

        string? linkedInUrl = null;
        if (request.LinkedInUrl is not null && !UrlAllowlist.TryNormalizeHttpUrl(request.LinkedInUrl, out linkedInUrl))
        {
            return BadRequest("linkedInUrl must be an absolute http/https URL.");
        }

        string? gitHubUrl = null;
        if (request.GitHubUrl is not null && !UrlAllowlist.TryNormalizeHttpUrl(request.GitHubUrl, out gitHubUrl))
        {
            return BadRequest("gitHubUrl must be an absolute http/https URL.");
        }

        List<ProofLink>? normalizedProofLinks = null;
        if (request.ProofLinks is not null)
        {
            normalizedProofLinks = new List<ProofLink>(request.ProofLinks.Count);

            for (var i = 0; i < request.ProofLinks.Count; i++)
            {
                var link = request.ProofLinks[i];
                if (string.IsNullOrWhiteSpace(link.Label) || link.Label.Length > 80)
                {
                    return BadRequest($"proofLinks[{i}].label is required and must be 80 characters or fewer.");
                }

                if (!UrlAllowlist.TryNormalizeHttpUrl(link.Url, out var normalizedProofUrl))
                {
                    return BadRequest($"proofLinks[{i}].url must be an absolute http/https URL.");
                }

                normalizedProofLinks.Add(new ProofLink
                {
                    Label = link.Label.Trim(),
                    Url = normalizedProofUrl
                });
            }
        }

        if (request.Headline is not null) portfolio.Headline = request.Headline;
        if (request.DisplayName is not null) portfolio.DisplayName = request.DisplayName;
        if (request.PositioningLine is not null) portfolio.PositioningLine = request.PositioningLine;
        if (request.PhotoUrl is not null) portfolio.PhotoUrl = photoUrl;
        if (request.CurrentRole is not null) portfolio.CurrentRole = request.CurrentRole;
        if (request.YearsExperience is not null) portfolio.YearsExperience = request.YearsExperience;
        if (request.CoreTechnologies is not null) portfolio.CoreTechnologies = request.CoreTechnologies;
        if (request.LinkedInUrl is not null) portfolio.LinkedInUrl = linkedInUrl;
        if (request.GitHubUrl is not null) portfolio.GitHubUrl = gitHubUrl;
        // Milestone 19 — once GitHubVerified, GitHubUrl is sourced only
        // from POST me/github/sync (a real Clerk-confirmed account), never
        // from a hand-typed PUT body. Silently ignoring rather than 400ing:
        // the profile form no longer renders an editable field for this
        // once verified, so a request carrying GitHubUrl here means either
        // a stale client or someone bypassing the UI — neither is worth
        // failing the rest of an otherwise-valid update over.
        if (request.GitHubUrl is not null && !portfolio.GitHubVerified) portfolio.GitHubUrl = gitHubUrl;
        if (request.Discipline is not null) portfolio.Discipline = request.Discipline.Value;
        if (normalizedProofLinks is not null) portfolio.ProofLinks = normalizedProofLinks;
        if (request.Visibility is not null) portfolio.Visibility = request.Visibility.Value;
        if (request.Discoverable is not null) portfolio.Discoverable = request.Discoverable.Value;
        if (request.NotificationEmail is not null) portfolio.NotificationEmail = request.NotificationEmail;

        await _db.SaveChangesAsync();

        return Ok(ToMyPortfolioDto(portfolio));
    }

    // POST /api/portfolio/me/github/sync — Milestone 19. Called by the
    // frontend right after Clerk's own createExternalAccount(oauth_github)
    // flow completes (dashboard/profile/page.tsx). Deliberately does not
    // trust anything the frontend reports about that flow's outcome — it
    // independently asks Clerk's Backend API, via the already-known Clerk
    // user id from this request's own validated JWT ("sub" claim, same one
    // TenantResolutionMiddleware reads for provisioning), whether that user
    // actually has a verified GitHub external account. If yes, GitHubUrl is
    // overwritten from the verified username (not merged with whatever was
    // there before) and GitHubVerified flips true. If no — not connected,
    // or a connection attempt didn't complete — GitHubVerified is set
    // false and GitHubUrl is left alone, so a creator who backs out of the
    // OAuth flow doesn't lose a link they'd already saved by hand before
    // this feature existed.
    [HttpPost("me/github/sync")]
    public async Task<IActionResult> SyncGitHubVerification(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var clerkUserId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(clerkUserId))
        {
            return Unauthorized("No Clerk user id resolved from the current token.");
        }

        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound();
        }

        var verifiedUsername = await _clerkUserService.GetVerifiedGitHubUsernameAsync(clerkUserId, cancellationToken);
        if (verifiedUsername is not null)
        {
            portfolio.GitHubUrl = $"https://github.com/{verifiedUsername}";
            portfolio.GitHubVerified = true;
        }
        else
        {
            portfolio.GitHubVerified = false;
        }

        await _db.SaveChangesAsync(cancellationToken);

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
        portfolio.GitHubVerified,
        portfolio.CvFileUrl,
        portfolio.Discipline,
        portfolio.ProofLinks,
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
                ViewerHash = _ipHashingService.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()),
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
            // Milestone 19 — surfaced publicly so Hero.tsx can show a
            // "Verified" mark next to the GitHub link. This is the actual
            // point of verification from a recruiter's perspective, not
            // just a creator-side nicety; the creator-authenticated
            // GitHubVerified above exists so the profile form can decide
            // whether the field is editable, this is the same fact shown
            // to the audience it's meant to reassure.
            githubVerified = portfolio.GitHubVerified,
            photoUrl = portfolio.PhotoUrl,
            // Multi-role expansion — discipline drives which label variant
            // Hero.tsx renders for a field (e.g. "Core technologies" vs.
            // "Core skills/tools"); proofLinks is the generalized
            // supplement to githubUrl above. Both public: this is exactly
            // the same data a recruiter is already trusted to see via the
            // rest of this response.
            discipline = portfolio.Discipline,
            proofLinks = portfolio.ProofLinks,
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
        [param: MaxLength(140)] string? Headline,
        [param: MaxLength(120)] string? DisplayName,
        [param: MaxLength(200)] string? PositioningLine,
        [param: MaxLength(2048)] string? PhotoUrl,
        [param: MaxLength(120)] string? CurrentRole,
        [param: MaxLength(80)] string? YearsExperience,
        [param: MaxLength(30)] List<string>? CoreTechnologies,
        [param: MaxLength(2048)] string? LinkedInUrl,
        [param: MaxLength(2048)] string? GitHubUrl,
        PortfolioDiscipline? Discipline,
        [param: MaxLength(20)] List<ProofLink>? ProofLinks,
        PortfolioVisibility? Visibility,
        bool? Discoverable,
        [param: EmailAddress, MaxLength(320)] string? NotificationEmail);
}
