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

    public record ReviewWaitlistRequest(
        [param: Required, MaxLength(24)] string Action,
        DateTime? ExpiresAt,
        [param: MaxLength(1000)] string? Notes);

    public record RevokeInviteRequest(
        [param: MaxLength(1000)] string? Notes);
}
