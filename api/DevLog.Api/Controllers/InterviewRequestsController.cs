using DevLog.Infrastructure.Interviews;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/portfolio/{slug}/interview-requests")]
public class InterviewRequestsController : ControllerBase
{
    private readonly InterviewRequestService _interviewRequestService;

    public InterviewRequestsController(InterviewRequestService interviewRequestService)
    {
        _interviewRequestService = interviewRequestService;
    }

    [EnableRateLimiting("InterviewRequestCreate")]
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

    [EnableRateLimiting("InterviewRequestVerify")]
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

    public record CreateInterviewRequestBody(
        [param: Required, EmailAddress, MaxLength(320)] string RecruiterEmail,
        [param: MaxLength(120)] string? RecruiterName,
        [param: Required, MaxLength(2000)] string Note);

    public record VerifyInterviewRequestBody(
        [param: Required, MaxLength(256)] string Token);
}
