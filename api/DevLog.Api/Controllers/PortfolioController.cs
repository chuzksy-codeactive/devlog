using DevLog.Domain.Entities;
using DevLog.Api.Validation;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Identity;
using DevLog.Infrastructure.MultiTenancy;
using DevLog.Infrastructure.Security;
using DevLog.Infrastructure.Storage;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevLog.Api.Controllers;

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
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly Regex OgImageMetaRegex = new(
        "<meta\\s+[^>]*(?:property|name)=[\"']og:image(?::secure_url)?[\"'][^>]*content=[\"'](?<url>[^\"']+)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PortfolioController(
        DevLogDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ClerkUserService clerkUserService,
        ILogger<PortfolioController> logger,
        IIpHashingService ipHashingService,
        IFileStorageProvider fileStorageProvider,
        IOptions<StorageOptions> storageOptions,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _clerkUserService = clerkUserService;
        _logger = logger;
        _ipHashingService = ipHashingService;
        _fileStorageProvider = fileStorageProvider;
        _storageOptions = storageOptions.Value;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPortfolio()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);

        if (portfolio is null)
        {
            return NotFound();
        }

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

    [AllowAnonymous]
    [EnableRateLimiting("PublicCvAccess")]
    [HttpGet("{slug}/cv-access-url")]
    public async Task<IActionResult> GetPublicCvAccessUrl(string slug, CancellationToken cancellationToken)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return NotFound("No public portfolio found at this slug.");
        }

        if (string.IsNullOrWhiteSpace(portfolio.CvFileUrl))
        {
            return NotFound("No CV is currently available for this portfolio.");
        }

        var minutes = Math.Clamp(_storageOptions.SignedUrlLifetimeMinutes, 1, 60);
        var lifetime = TimeSpan.FromMinutes(minutes);
        var readUrl = await _fileStorageProvider.GetReadUrlAsync(portfolio.CvFileUrl, lifetime, cancellationToken);

        _logger.LogInformation(
            "Issued public CV access URL for slug {Slug} with lifetime {LifetimeSeconds}s.",
            slug,
            (int)lifetime.TotalSeconds);

        return Ok(new
        {
            cvAccessUrl = readUrl,
            expiresInSeconds = (int)lifetime.TotalSeconds
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyPortfolio([FromBody] UpdatePortfolioRequest request, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

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
        if (!string.IsNullOrWhiteSpace(photoUrl) && IsGooglePhotosShareUrl(photoUrl))
        {
            var resolvedPhotoUrl = await ResolveGooglePhotosImageUrlAsync(photoUrl, cancellationToken);
            if (resolvedPhotoUrl is null)
            {
                return BadRequest("photoUrl must be a direct image URL. Google Photos share links could not be resolved to a direct image.");
            }

            photoUrl = resolvedPhotoUrl;
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
        if (request.GitHubUrl is not null && !portfolio.GitHubVerified) portfolio.GitHubUrl = gitHubUrl;
        if (request.Discipline is not null) portfolio.Discipline = request.Discipline.Value;
        if (normalizedProofLinks is not null) portfolio.ProofLinks = normalizedProofLinks;
        if (request.Visibility is not null) portfolio.Visibility = request.Visibility.Value;
        if (request.Discoverable is not null) portfolio.Discoverable = request.Discoverable.Value;
        if (request.NotificationEmail is not null) portfolio.NotificationEmail = request.NotificationEmail;

        await _db.SaveChangesAsync();

        return Ok(ToMyPortfolioDto(portfolio));
    }

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
            githubVerified = portfolio.GitHubVerified,
            hasCv = !string.IsNullOrWhiteSpace(portfolio.CvFileUrl),
            photoUrl = portfolio.PhotoUrl,
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

    private static bool IsGooglePhotosShareUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return host == "photos.app.goo.gl" || host == "photos.google.com";
    }

    private async Task<string?> ResolveGooglePhotosImageUrlAsync(string shareUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, shareUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", "DevLog/1.0 (+https://devlog.local)");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return shareUrl;
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = OgImageMetaRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var candidate = System.Net.WebUtility.HtmlDecode(match.Groups["url"].Value).Trim();
        if (!UrlAllowlist.TryNormalizeHttpUrl(candidate, out var normalized))
        {
            return null;
        }

        return normalized;
    }
}
