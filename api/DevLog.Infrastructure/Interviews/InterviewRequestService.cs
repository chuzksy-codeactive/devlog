using DevLog.Domain.Entities;
using DevLog.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevLog.Infrastructure.Interviews;

public enum RequestInterviewOutcome
{
    Created,
    PortfolioNotFound
}

public record RequestInterviewResult(RequestInterviewOutcome Outcome, Guid? Id = null);

// The last Phase 1 core feature per the MVP roadmap — "a simpler,
// self-contained addition once the rest exists." Mirrors AskAiService's
// shape for the anonymous half (portfolio lookup + visibility check, no
// tenant session context set on the connection — see CreateAsync) and
// EntriesController's shape for the creator-authenticated half
// (Accept/Decline run under the caller's own tenant session, RLS scopes
// them, no explicit WHERE tenant_id needed).
public class InterviewRequestService
{
    private readonly DevLogDbContext _db;
    private readonly IEmailProvider _emailProvider;
    private readonly ILogger<InterviewRequestService> _logger;

    public InterviewRequestService(DevLogDbContext db, IEmailProvider emailProvider, ILogger<InterviewRequestService> logger)
    {
        _db = db;
        _emailProvider = emailProvider;
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
            CreatedAt = DateTime.UtcNow
        };

        _db.InterviewRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        // Architecture doc Section 6: "On insert, a job sends a
        // notification email to the creator." Sent synchronously, inline on
        // this request — no Hangfire queue exists yet, same "synchronous
        // for now, async later" simplification the sanitization pipeline
        // made. Best-effort: see NotifyCreatorAsync's doc comment for why
        // failures here never surface to the recruiter.
        await NotifyCreatorAsync(portfolio, request, cancellationToken);

        return new RequestInterviewResult(RequestInterviewOutcome.Created, request.Id);
    }

    // Deliberately swallows failures: the request is already durably saved
    // by the time this runs, so a recruiter's submission must still succeed
    // even if Postmark is down, misconfigured, or the creator simply hasn't
    // set Portfolio.NotificationEmail yet. The creator can always find the
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
