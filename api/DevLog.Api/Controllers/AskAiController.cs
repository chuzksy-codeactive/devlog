using System.Security.Cryptography;
using System.Text;
using DevLog.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevLog.Api.Controllers;

// Deliberately anonymous — no [Authorize] — per the MVP roadmap's Phase 1
// scope: "anonymous browsing of published portfolios... single-candidate
// Ask AI." Recruiter accounts/login are a Phase 2 concern (roadmap: "the
// lightweight per-request record they were in Phase 1").
[ApiController]
[Route("api/portfolio/{slug}/ask")]
public class AskAiController : ControllerBase
{
    private readonly AskAiService _askAiService;

    public AskAiController(AskAiService askAiService)
    {
        _askAiService = askAiService;
    }

    // Milestone 20 — "AskAi" policy (Program.cs), partitioned by (IP, slug).
    // Layered on top of AskAiService's own per-tenant hourly cap below —
    // that protects a tenant's shared quota from being drained by anyone;
    // this stops a single source from doing the draining fast enough for
    // the DB-backed cap to matter.
    [EnableRateLimiting("AskAi")]
    [HttpPost]
    public async Task<IActionResult> Ask(string slug, [FromBody] AskAiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("question is required.");
        }

        var askerHash = HashRemoteIp(HttpContext.Connection.RemoteIpAddress?.ToString());

        var result = await _askAiService.AskAsync(slug, request.Question, askerHash, cancellationToken);

        return result.Outcome switch
        {
            AskAiOutcome.Answered => Ok(new { answer = result.Answer }),
            AskAiOutcome.PortfolioNotFound => NotFound("No public portfolio found at this slug."),
            AskAiOutcome.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests,
                "This candidate has hit the hourly question limit. Try again later."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // Not a recruiter identity (none exists yet) — just enough to let the
    // rate cap and any future abuse review distinguish "many different
    // askers" from "one asker retrying," without storing a raw IP address.
    private static string? HashRemoteIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes);
    }

    public record AskAiRequest(string Question);
}
