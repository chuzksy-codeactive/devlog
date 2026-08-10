using System.ComponentModel.DataAnnotations;
using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.BetaAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/beta-access/admin")]
[Authorize]
public class BetaAccessAdminController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly IOptions<PrivateBetaOptions> _privateBetaOptions;

    public BetaAccessAdminController(
        DevLogDbContext db,
        IOptions<PrivateBetaOptions> privateBetaOptions)
    {
        _db = db;
        _privateBetaOptions = privateBetaOptions;
    }

    [HttpGet("waitlist")]
    public async Task<IActionResult> GetWaitlist(
        [FromQuery] string? status,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentAdminEmail(out _))
        {
            return Forbid();
        }

        if (!TryParseWaitlistStatus(status, out var parsedStatus))
        {
            return BadRequest("Invalid waitlist status.");
        }

        var size = Math.Clamp(take, 1, 200);
        var query = _db.WaitlistSubmissions.AsNoTracking();
        if (parsedStatus is WaitlistSubmissionStatus typedStatus)
        {
            query = query.Where(x => x.Status == typedStatus);
        }

        var rows = await query
            .OrderBy(x => x.Status == WaitlistSubmissionStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(size)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.Name,
                x.Reason,
                x.UseCase,
                x.Referrer,
                status = x.Status.ToString(),
                x.CreatedAt,
                x.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites(
        [FromQuery] string? status,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentAdminEmail(out _))
        {
            return Forbid();
        }

        if (!TryParseInviteStatus(status, out var parsedStatus))
        {
            return BadRequest("Invalid invite status.");
        }

        var size = Math.Clamp(take, 1, 300);
        var query = _db.InviteAllowlistEntries.AsNoTracking();
        if (parsedStatus is InviteAllowlistStatus typedStatus)
        {
            query = query.Where(x => x.Status == typedStatus);
        }

        var rows = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Take(size)
            .Select(x => new
            {
                x.Id,
                x.Email,
                status = x.Status.ToString(),
                x.InvitedBy,
                x.ExpiresAt,
                x.Notes,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost("waitlist/{id:guid}/review")]
    public async Task<IActionResult> ReviewWaitlist(
        Guid id,
        [FromBody] ReviewWaitlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentAdminEmail(out var adminEmail))
        {
            return Forbid();
        }

        if (!TryParseReviewAction(request.Action, out var approved))
        {
            return BadRequest("Action must be approve or reject.");
        }

        var waitlist = await _db.WaitlistSubmissions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (waitlist is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        waitlist.Status = approved ? WaitlistSubmissionStatus.Approved : WaitlistSubmissionStatus.Rejected;
        waitlist.ReviewedAt = now;

        InviteAllowlistEntry? invite = null;
        if (approved)
        {
            invite = await _db.InviteAllowlistEntries
                .FirstOrDefaultAsync(x => x.NormalizedEmail == waitlist.NormalizedEmail, cancellationToken);

            var expiresAt = NormalizeUtcDateTime(request.ExpiresAt);
            if (invite is null)
            {
                invite = new InviteAllowlistEntry
                {
                    Id = Guid.NewGuid(),
                    Email = waitlist.Email,
                    NormalizedEmail = waitlist.NormalizedEmail,
                    Status = InviteAllowlistStatus.Active,
                    InvitedBy = adminEmail,
                    ExpiresAt = expiresAt,
                    Notes = NullIfWhiteSpace(request.Notes),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.InviteAllowlistEntries.Add(invite);
            }
            else
            {
                invite.Email = waitlist.Email;
                invite.Status = InviteAllowlistStatus.Active;
                invite.InvitedBy = adminEmail;
                invite.ExpiresAt = expiresAt;
                invite.Notes = NullIfWhiteSpace(request.Notes);
                invite.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            waitlistId = waitlist.Id,
            waitlistStatus = waitlist.Status.ToString(),
            inviteStatus = invite?.Status.ToString()
        });
    }

    [HttpPost("invites/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeInvite(
        Guid id,
        [FromBody] RevokeInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentAdminEmail(out _))
        {
            return Forbid();
        }

        var invite = await _db.InviteAllowlistEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invite is null)
        {
            return NotFound();
        }

        invite.Status = InviteAllowlistStatus.Revoked;
        invite.Notes = NullIfWhiteSpace(request.Notes) ?? invite.Notes;
        invite.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { inviteId = invite.Id, inviteStatus = invite.Status.ToString() });
    }

    [HttpGet("analytics/users")]
    public async Task<IActionResult> GetUserActivityAnalytics(
        [FromQuery] int days = 30,
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentAdminEmail(out _))
        {
            return Forbid();
        }

        var windowDays = Math.Clamp(days, 7, 180);
        var size = Math.Clamp(take, 5, 100);
        var now = DateTime.UtcNow;
        var since = now.AddDays(-windowDays);

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.CreatedAt,
                DisplayName = t.Portfolio != null ? t.Portfolio.DisplayName : null
            })
            .ToListAsync(cancellationToken);

        var viewsByTenant = await _db.PortfolioViewEvents
            .AsNoTracking()
            .Where(x => x.ViewedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.ViewedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var aiQueriesByTenant = await _db.AiQueryLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var rawEntriesByTenant = await _db.RawEntries
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var publishedByTenant = await _db.PublishedEntries
            .AsNoTracking()
            .Where(x => x.PublishedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.PublishedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var resumeImportsByTenant = await _db.ResumeImportLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var sanitizationsByTenant = await _db.SanitizationRunLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var interviewVerifiedByTenant = await _db.InterviewRequests
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.EmailVerified)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => new ActivityStat(x.Count, x.LastAt), cancellationToken);

        var experiencesByTenant = await _db.Experiences
            .AsNoTracking()
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var rows = tenants.Select(tenant =>
        {
            var views = viewsByTenant.TryGetValue(tenant.Id, out var viewStat) ? viewStat : ActivityStat.Empty;
            var aiQueries = aiQueriesByTenant.TryGetValue(tenant.Id, out var aiStat) ? aiStat : ActivityStat.Empty;
            var rawEntries = rawEntriesByTenant.TryGetValue(tenant.Id, out var rawStat) ? rawStat : ActivityStat.Empty;
            var published = publishedByTenant.TryGetValue(tenant.Id, out var publishedStat) ? publishedStat : ActivityStat.Empty;
            var resumeImports = resumeImportsByTenant.TryGetValue(tenant.Id, out var importStat) ? importStat : ActivityStat.Empty;
            var sanitizations = sanitizationsByTenant.TryGetValue(tenant.Id, out var sanitizeStat) ? sanitizeStat : ActivityStat.Empty;
            var interviewVerified = interviewVerifiedByTenant.TryGetValue(tenant.Id, out var interviewStat) ? interviewStat : ActivityStat.Empty;
            var experienceCount = experiencesByTenant.TryGetValue(tenant.Id, out var count) ? count : 0;

            var score =
                (published.Count * 6) +
                (rawEntries.Count * 3) +
                (resumeImports.Count * 3) +
                (sanitizations.Count * 2) +
                (aiQueries.Count * 2) +
                views.Count +
                (interviewVerified.Count * 2) +
                experienceCount;

            var possibleAbuse = aiQueries.Count >= 20 && rawEntries.Count == 0 && published.Count == 0;
            var level = score >= 35
                ? "HighlyActive"
                : score >= 12
                    ? "ModeratelyActive"
                    : "LowActivity";

            var lastActivityAt = MaxTimestamp(
                views.LastAt,
                aiQueries.LastAt,
                rawEntries.LastAt,
                published.LastAt,
                resumeImports.LastAt,
                sanitizations.LastAt,
                interviewVerified.LastAt);

            return new UserActivityRow(
                tenant.Id,
                tenant.Slug,
                string.IsNullOrWhiteSpace(tenant.DisplayName) ? null : tenant.DisplayName,
                tenant.CreatedAt,
                level,
                score,
                possibleAbuse,
                lastActivityAt,
                experienceCount,
                rawEntries.Count,
                published.Count,
                resumeImports.Count,
                sanitizations.Count,
                views.Count,
                aiQueries.Count,
                interviewVerified.Count);
        }).ToList();

        var mostActive = rows
            .OrderByDescending(x => x.ActivityScore)
            .ThenByDescending(x => x.PublishedEntries30d)
            .ThenByDescending(x => x.RawEntries30d)
            .ThenByDescending(x => x.LastActivityAt)
            .Take(size)
            .ToList();

        var leastActive = rows
            .OrderBy(x => x.ActivityScore)
            .ThenBy(x => x.LastActivityAt.HasValue ? 0 : 1)
            .ThenBy(x => x.LastActivityAt)
            .Take(size)
            .ToList();

        return Ok(new
        {
            windowDays,
            generatedAt = now,
            tenantCount = rows.Count,
            highlyActiveCount = rows.Count(x => x.ActivityLevel == "HighlyActive"),
            moderatelyActiveCount = rows.Count(x => x.ActivityLevel == "ModeratelyActive"),
            lowActivityCount = rows.Count(x => x.ActivityLevel == "LowActivity"),
            possibleAbuseCount = rows.Count(x => x.PossibleAbuse),
            mostActive,
            leastActive
        });
    }

    private bool TryGetCurrentAdminEmail(out string email)
    {
        var options = _privateBetaOptions.Value;
        email = ResolveCurrentUserEmail(options.EmailClaimTypes) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalized = NormalizeEmail(email);
        return options.AdminEmails.Any(admin => NormalizeEmail(admin) == normalized);
    }

    private string? ResolveCurrentUserEmail(IEnumerable<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryParseReviewAction(string? action, out bool approved)
    {
        approved = false;
        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            approved = true;
            return true;
        }

        if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseWaitlistStatus(string? status, out WaitlistSubmissionStatus? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<WaitlistSubmissionStatus>(status, true, out var typed))
        {
            parsed = typed;
            return true;
        }

        return false;
    }

    private static bool TryParseInviteStatus(string? status, out InviteAllowlistStatus? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<InviteAllowlistStatus>(status, true, out var typed))
        {
            parsed = typed;
            return true;
        }

        return false;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? NormalizeUtcDateTime(DateTime? value)
    {
        if (value is not DateTime timestamp)
        {
            return null;
        }

        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
    }

    private static DateTime? MaxTimestamp(params DateTime?[] values) =>
        values.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty().Max() switch
        {
            var value when value == default => null,
            var value => value
        };

    private readonly record struct ActivityStat(int Count, DateTime? LastAt)
    {
        public static readonly ActivityStat Empty = new(0, null);
    }

    private readonly record struct UserActivityRow(
        Guid TenantId,
        string Slug,
        string? DisplayName,
        DateTime TenantCreatedAt,
        string ActivityLevel,
        int ActivityScore,
        bool PossibleAbuse,
        DateTime? LastActivityAt,
        int ExperienceCount,
        int RawEntries30d,
        int PublishedEntries30d,
        int ResumeImports30d,
        int Sanitizations30d,
        int PortfolioViews30d,
        int AiQueries30d,
        int InterviewRequestsVerified30d);

    public record ReviewWaitlistRequest(
        [param: Required, MaxLength(24)] string Action,
        DateTime? ExpiresAt,
        [param: MaxLength(1000)] string? Notes);

    public record RevokeInviteRequest(
        [param: MaxLength(1000)] string? Notes);
}
