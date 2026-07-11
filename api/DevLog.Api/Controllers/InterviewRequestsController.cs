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

    public record CreateInterviewRequestBody(string RecruiterEmail, string? RecruiterName, string Note);
}
