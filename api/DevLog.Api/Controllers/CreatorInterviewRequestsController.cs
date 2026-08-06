using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Interviews;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/interview-requests")]
[Authorize]
public class CreatorInterviewRequestsController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly InterviewRequestService _interviewRequestService;

    public CreatorInterviewRequestsController(DevLogDbContext db, InterviewRequestService interviewRequestService)
    {
        _db = db;
        _interviewRequestService = interviewRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInbox(CancellationToken cancellationToken)
    {
        var requests = await _db.InterviewRequests
            .Where(r => r.EmailVerified)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToDto));
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(
        Guid id,
        [FromBody] AcceptInterviewRequestBody request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ContactInstructions))
        {
            return BadRequest("contactInstructions is required to accept — it's what gets revealed to the recruiter.");
        }

        var updated = await _interviewRequestService.AcceptAsync(id, request.ContactInstructions.Trim(), cancellationToken);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        var updated = await _interviewRequestService.DeclineAsync(id, cancellationToken);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    private static object ToDto(InterviewRequest r) => new
    {
        r.Id,
        r.RecruiterEmail,
        r.RecruiterName,
        r.Note,
        r.Status,
        r.ContactInstructions,
        r.CreatedAt,
        r.RespondedAt
    };

    public record AcceptInterviewRequestBody(
        [param: Required, MaxLength(2000)] string ContactInstructions);
}
