using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Interviews;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Creator-only inbox for requests that came in through
// InterviewRequestsController's anonymous endpoint. No dashboard UI ships
// in Phase 1 (roadmap: "instrument everything, ship no dashboard... query
// the tables directly for now") — this endpoint exists so the creator has
// a way to see and act on requests via curl/Postman today, same pattern as
// Milestone 2's EntriesController smoke test before any UI existed for it.
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

    // GET /api/interview-requests — no WHERE tenant_id clause, same
    // deliberate pattern as PortfolioController/EntriesController: RLS's
    // tenant_isolation policy is what actually scopes this to the caller's
    // own tenant.
    //
    // WHERE EmailVerified is real filtering, though, not just defense in
    // depth (Milestone 10) — an unconfirmed request shouldn't exist in the
    // creator's world at all yet. A fabricated or mistyped recruiter email
    // that never completes verification stays invisible here permanently,
    // not just until it "expires" — this is the actual anti-abuse control,
    // not a cosmetic filter.
    [HttpGet]
    public async Task<IActionResult> GetInbox(CancellationToken cancellationToken)
    {
        var requests = await _db.InterviewRequests
            .Where(r => r.EmailVerified)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToDto));
    }

    // POST /api/interview-requests/{id}/accept — the only path that ever
    // populates ContactInstructions. Architecture doc Section 6: accepting
    // "reveals a scheduling link or contact method of the creator's
    // choosing" — supplied here, not defaulted from Portfolio.LinkedInUrl,
    // so the creator can pick something different per request if they want.
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
