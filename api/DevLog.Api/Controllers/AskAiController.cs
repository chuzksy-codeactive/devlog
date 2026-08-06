using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.Security;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/portfolio/{slug}/ask")]
public class AskAiController : ControllerBase
{
    private readonly AskAiService _askAiService;
    private readonly IIpHashingService _ipHashingService;

    public AskAiController(AskAiService askAiService, IIpHashingService ipHashingService)
    {
        _askAiService = askAiService;
        _ipHashingService = ipHashingService;
    }

    [EnableRateLimiting("AskAi")]
    [HttpPost]
    public async Task<IActionResult> Ask(string slug, [FromBody] AskAiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("question is required.");
        }

        var askerHash = _ipHashingService.HashIp(HttpContext.Connection.RemoteIpAddress?.ToString());

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

    public record AskAiRequest(
        [param: Required, MaxLength(1000)] string Question);
}
