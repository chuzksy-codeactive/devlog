using DevLog.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly DevLogDbContext _db;

    public PortfolioController(DevLogDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPortfolio()
    {
        // No explicit WHERE tenant_id clause here — deliberately. This is
        // exactly what TenantIsolationTests proves: RLS, not this query,
        // is what stops another tenant's row from coming back.
        var portfolio = await _db.Portfolios.FirstOrDefaultAsync();

        if (portfolio is null)
        {
            return NotFound();
        }

        return Ok(portfolio);
    }
}
