using System.Security.Cryptography;
using DevLog.Domain.Entities;
using DevLog.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Interviews;

public enum RequestInterviewOutcome
{
    Created,
    PortfolioNotFound
}

public record RequestInterviewResult(RequestInterviewOutcome Outcome, Guid? Id = null);

public enum VerifyEmailOutcome
{
    Verified,
    AlreadyVerified,
    InvalidOrExpired,
    NotFound
}

// The last Phase 1 core feature per the MVP roadmap — "a simpler,
// self-contained addition once the rest exists." Mirrors AskAiService's
// shape for the anonymous half (portfolio lookup + visibility check, no
// tenant session context set on the connection — see CreateAsync) and
// EntriesController's shape for the creator-authenticated half
// (Accept/Decline run under the caller's own tenant session, RLS scopes
// them, no explicit WHERE tenant_id needed).
//
// Milestone 10 added a verify-before-notify step: CreateAsync no longer
// notifies the creator directly — it emails the recruiter a one-time
// confirmation link instead, and only VerifyEmailAsync (reached by clicking
// that link) triggers the creator notification. A submitted-but-unverified
// request is stored but inert: invisible to the creator's inbox
// (CreatorInterviewRequestsController filters on EmailVerified), and never
// notified about. This is the "recruiter email verification" Phase 1
// security item — a cheap control against a fabricated or mistyped email
// address ever reaching a creator as if it were real.
public class InterviewRequestService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly DevLogDbContext _db;
    private readonly IEmailProvider _emailProvider;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<InterviewRequestService> _logger;

    public InterviewRequestService(
        DevLogDbContext db,
        IEmailProvider emailProvider,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<InterviewRequestService> logger)
    {
        _db = db;
        _emailProvider = emailProvider;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<RequestInterviewResult> CreateAsync(
        string slug,
        string recruiterEmail,
        string? recruiterName,
        string note,
        CancellationToken cancellationToken = default)
    {
        // No tenant session context is set for this request (anonymous —
        // recruiters have no login in Phase 1), so this read only returns a
        // row because of sql/002's public-read pattern extended to
        // portfolios/tenants, and the INSERT below only succeeds because of
        // sql/003's public_insert policy. Visibility is checked again here
        // in the app layer too, same defense-in-depth principle as
        // AskAiService.
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return new RequestInterviewResult(RequestInterviewOutcome.PortfolioNotFound);
        }

        var request = new InterviewRequest
        {
            Id = Guid.NewGuid(),
            TenantId = portfolio.TenantId,
            RecruiterEmail = recruiterEmail,
            RecruiterName = recruiterName,
            Note = note,
            Status = InterviewRequestStatus.Pending,
            EmailVerified = false,
            VerificationToken = RandomNumberGenerator.GetHexString(64),
            VerificationTokenExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime),
            CreatedAt = DateTime.UtcNow
        };

        _db.InterviewRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        // Not the creator notification (that moved to VerifyEmailAsync) —
        // this goes to the recruiter, confirming the address they typed is
        // one they actually control. Best-effort for the same reason as
        // NotifyCreatorAsync: the request is already durably saved, so a
        // Postmark hiccup here shouldn't read as "the submission failed" —
        // though practically, if this email never arrives, the request
        // just stays unverified and inert forever, which is a real (if
        // quiet) failure mode worth knowing about if recruiters ever report
        // "I submitted a request and nothing happened."
        await SendVerificationEmailAsync(slug, request, cancellationToken);

        return new RequestInterviewResult(RequestInterviewOutcome.Created, request.Id);
    }

    private async Task SendVerificationEmailAsync(string slug, InterviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // slug is threaded through purely so the frontend confirmation
            // page can call back into
            // POST /api/portfolio/{slug}/interview-requests/{id}/verify,
            // which takes slug as a route segment. The backend lookup itself
            // (VerifyEmailAsync) never uses slug — the id+token pair is
            // already globally unique and self-authenticating.
            var verifyUrl =
                $"{_frontendOptions.BaseUrl.TrimEnd('/')}/verify-interview-request?slug={Uri.EscapeDataString(slug)}&requestId={request.Id}&token={request.VerificationToken}";

            await _emailProvider.SendAsync(
                request.RecruiterEmail,
                "Confirm your DevLog interview request",
                "Click the link below to confirm this is your email address and send your interview request:\n\n" +
                $"{verifyUrl}\n\n" +
                "This link expires in 24 hours. If you didn't request this, you can ignore this email.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send verification email for interview request {RequestId}",
                request.Id);
        }
    }

    public async Task<VerifyEmailOutcome> VerifyEmailAsync(Guid requestId, string token, CancellationToken cancellationToken = default)
    {
        // No WHERE tenant_id clause and no [Authorize] on the endpoint that
        // calls this — deliberate, this runs for an anonymous recruiter
        // clicking a link from their inbox. The token itself is the only
        // credential; sql/003's tenant_isolation policy doesn't apply here
        // since no app.current_tenant is ever set on this connection.
        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return VerifyEmailOutcome.NotFound;
        }

        if (request.EmailVerified)
        {
            return VerifyEmailOutcome.AlreadyVerified;
        }

        if (request.VerificationToken is null
            || !string.Equals(request.VerificationToken, token, StringComparison.Ordinal)
            || request.VerificationTokenExpiresAt is not DateTime expiresAt
            || expiresAt < DateTime.UtcNow)
        {
            return VerifyEmailOutcome.InvalidOrExpired;
        }

        request.EmailVerified = true;
        request.VerificationToken = null;
        request.VerificationTokenExpiresAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        // This is the point where the creator actually finds out — moved
        // here from CreateAsync in Milestone 10. A fabricated or mistyped
        // email address now never reaches the creator's inbox at all,
        // since it can never complete this step.
        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == request.TenantId, cancellationToken);
        if (portfolio is not null)
        {
            await NotifyCreatorAsync(portfolio, request, cancellationToken);
        }

        return VerifyEmailOutcome.Verified;
    }

    // Deliberately swallows failures: the request is already durably saved
    // by the time this runs, so email verification must still succeed even
    // if Postmark is down, misconfigured, or the creator simply hasn't set
    // Portfolio.NotificationEmail yet. The creator can always find the
    // request via GET /api/interview-requests regardless of whether this
    // email went out — this is a convenience, not the source of truth.
    private async Task NotifyCreatorAsync(Portfolio portfolio, InterviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portfolio.NotificationEmail))
        {
            return;
        }

        try
        {
            var fromLine = string.IsNullOrWhiteSpace(request.RecruiterName)
                ? request.RecruiterEmail
                : $"{request.RecruiterName} ({request.RecruiterEmail})";

            await _emailProvider.SendAsync(
                portfolio.NotificationEmail,
                "New interview request on DevLog",
                $"{fromLine} sent you an interview request:\n\n\"{request.Note}\"\n\n" +
                "Review, accept, or decline it via GET/POST /api/interview-requests.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send interview request notification email for request {RequestId}",
                request.Id);
        }
    }

    public async Task<InterviewRequest?> AcceptAsync(
        Guid id,
        string contactInstructions,
        CancellationToken cancellationToken = default)
    {
        // No WHERE tenant_id clause — deliberate, same pattern as
        // EntriesController.GetEntry. RLS's tenant_isolation policy is what
        // actually stops one creator from accepting another tenant's
        // request; this query simply returns null if that policy hides the
        // row.
        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        request.Status = InterviewRequestStatus.Accepted;
        request.ContactInstructions = contactInstructions;
        request.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<InterviewRequest?> DeclineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        request.Status = InterviewRequestStatus.Declined;
        request.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return request;
    }
}
