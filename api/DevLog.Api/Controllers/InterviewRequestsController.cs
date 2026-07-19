using DevLog.Infrastructure.Interviews;
using Microsoft.AspNetCore.Mvc;

namespace DevLog.Api.Controllers;

// Deliberately anonymous — no [Authorize] — same reasoning as
// AskAiController: the MVP roadmap's Phase 1 scope is "anonymous
// browsing... Request Interview," and recruiter accounts/login don't exist
// until Phase 2 billing. The creator-facing half (inbox, accept, decline)
// lives in CreatorInterviewRequestsController instead, since it needs the
// opposite trust boundary.
[ApiController]
[Route("api/portfolio/{slug}/interview-requests")]
public class InterviewRequestsController : ControllerBase
{
    private readonly InterviewRequestService _interviewRequestService;

    public InterviewRequestsController(InterviewRequestService interviewRequestService)
    {
        _interviewRequestService = interviewRequestService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string slug,
        [FromBody] CreateInterviewRequestBody request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecruiterEmail) || !request.RecruiterEmail.Contains('@'))
        {
            return BadRequest("A valid recruiterEmail is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest("note is required.");
        }

        var result = await _interviewRequestService.CreateAsync(
            slug,
            request.RecruiterEmail.Trim(),
            string.IsNullOrWhiteSpace(request.RecruiterName) ? null : request.RecruiterName.Trim(),
            request.Note.Trim(),
            cancellationToken);

        return result.Outcome switch
        {
            RequestInterviewOutcome.Created => Created(string.Empty, new { id = result.Id }),
            RequestInterviewOutcome.PortfolioNotFound => NotFound("No public portfolio found at this slug."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // POST /api/portfolio/{slug}/interview-requests/{id}/verify — reached by
    // a recruiter clicking the confirmation link in their email
    // (InterviewRequestService.SendVerificationEmailAsync), via the
    // frontend's /verify-interview-request page. Deliberately anonymous,
    // same as Create above — the token itself is the credential, not a
    // session. `slug` isn't used for the lookup (the request id + token
    // pair is already globally unique and self-authenticating) but stays in
    // the route for consistency with the rest of this controller.
    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(
        string slug,
        Guid id,
        [FromBody] VerifyInterviewRequestBody body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Token))
        {
            return BadRequest("token is required.");
        }

        var outcome = await _interviewRequestService.VerifyEmailAsync(id, body.Token, cancellationToken);

        return outcome switch
        {
            VerifyEmailOutcome.Verified => Ok(new { verified = true }),
            VerifyEmailOutcome.AlreadyVerified => Ok(new { verified = true, alreadyVerified = true }),
            VerifyEmailOutcome.InvalidOrExpired => BadRequest("This confirmation link is invalid or has expired."),
            VerifyEmailOutcome.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public record CreateInterviewRequestBody(string RecruiterEmail, string? RecruiterName, string Note);
    public record VerifyInterviewRequestBody(string Token);
}
