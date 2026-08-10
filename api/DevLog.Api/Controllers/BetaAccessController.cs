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
[Route("api/beta-access")]
[AllowAnonymous]
public class BetaAccessController : ControllerBase
{
    private readonly IBetaAccessService _betaAccessService;
    private readonly IOptions<PrivateBetaOptions> _privateBetaOptions;
    private readonly DevLogDbContext _db;

    public BetaAccessController(
        IBetaAccessService betaAccessService,
        IOptions<PrivateBetaOptions> privateBetaOptions,
        DevLogDbContext db)
    {
        _betaAccessService = betaAccessService;
        _privateBetaOptions = privateBetaOptions;
        _db = db;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _betaAccessService.EvaluateCurrentUserAsync(cancellationToken);
        var waitlistStatus = await GetLatestWaitlistStatusAsync(result.ResolvedEmail, cancellationToken);

        var canAccessCreatorFeatures = result.Outcome is BetaAccessOutcome.Allowed or BetaAccessOutcome.BetaDisabled;
        return Ok(new
        {
            privateBetaEnabled = _privateBetaOptions.Value.Enabled,
            outcome = result.Outcome.ToString(),
            canAccessCreatorFeatures,
            resolvedEmail = result.ResolvedEmail,
            waitlistSubmissionStatus = waitlistStatus?.ToString()
        });
    }

    [HttpPost("waitlist")]
    public async Task<IActionResult> SubmitWaitlist(
        [FromBody] SubmitWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        var status = await _betaAccessService.EvaluateCurrentUserAsync(cancellationToken);
        var email = !string.IsNullOrWhiteSpace(status.ResolvedEmail)
            ? status.ResolvedEmail
            : request.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email is required.");
        }

        var emailValidator = new EmailAddressAttribute();
        if (!emailValidator.IsValid(email))
        {
            return BadRequest("A valid email is required.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var latest = await _db.WaitlistSubmissions
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.Status == WaitlistSubmissionStatus.Pending)
        {
            return Ok(new
            {
                submitted = false,
                alreadySubmitted = true,
                status = latest.Status.ToString()
            });
        }

        var submission = new WaitlistSubmission
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = NullIfWhiteSpace(request.Name),
            Reason = NullIfWhiteSpace(request.Reason),
            UseCase = NullIfWhiteSpace(request.UseCase),
            Referrer = NullIfWhiteSpace(request.Referrer),
            Status = WaitlistSubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.WaitlistSubmissions.Add(submission);
        await _db.SaveChangesAsync(cancellationToken);

        return Created(string.Empty, new
        {
            submitted = true,
            alreadySubmitted = false,
            status = submission.Status.ToString()
        });
    }

    private async Task<WaitlistSubmissionStatus?> GetLatestWaitlistStatusAsync(
        string? resolvedEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resolvedEmail))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(resolvedEmail);
        return await _db.WaitlistSubmissions
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (WaitlistSubmissionStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public record SubmitWaitlistRequest(
        [param: MaxLength(320)] string? Email,
        [param: MaxLength(160)] string? Name,
        [param: MaxLength(2000)] string? Reason,
        [param: MaxLength(1200)] string? UseCase,
        [param: MaxLength(320)] string? Referrer);
}
