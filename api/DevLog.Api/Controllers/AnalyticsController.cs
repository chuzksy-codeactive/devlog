using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly DevLogDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AnalyticsController(DevLogDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var now = DateTime.UtcNow;
        var since7d = now.AddDays(-7);
        var since30d = now.AddDays(-30);

        var viewTimestamps = await _db.PortfolioViewEvents
            .Where(v => v.TenantId == tenantId && v.ViewedAt >= since30d)
            .Select(v => v.ViewedAt)
            .ToListAsync(cancellationToken);

        var queryTimestamps = await _db.AiQueryLogs
            .Where(q => q.TenantId == tenantId && q.CreatedAt >= since30d)
            .Select(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        var viewsTotal = await _db.PortfolioViewEvents.CountAsync(v => v.TenantId == tenantId, cancellationToken);
        var queriesTotal = await _db.AiQueryLogs.CountAsync(q => q.TenantId == tenantId, cancellationToken);

        var interviewRequests = await _db.InterviewRequests
            .Where(r => r.TenantId == tenantId)
            .Select(r => new { r.Status, r.EmailVerified })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            viewsTotal,
            views7d = viewTimestamps.Count(t => t >= since7d),
            views30d = viewTimestamps.Count,
            queriesTotal,
            queries7d = queryTimestamps.Count(t => t >= since7d),
            queries30d = queryTimestamps.Count,
            interviewRequestsVerified = interviewRequests.Count(r => r.EmailVerified),
            interviewRequestsPending = interviewRequests.Count(r => r.EmailVerified && r.Status == InterviewRequestStatus.Pending),
            interviewRequestsAccepted = interviewRequests.Count(r => r.EmailVerified && r.Status == InterviewRequestStatus.Accepted),
            viewsByDay = BucketByDay(viewTimestamps, since30d, now),
            queriesByDay = BucketByDay(queryTimestamps, since30d, now)
        });
    }

    private static List<DailyCount> BucketByDay(List<DateTime> timestamps, DateTime since, DateTime now)
    {
        var counts = timestamps
            .GroupBy(t => DateOnly.FromDateTime(t))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<DailyCount>();
        for (var day = DateOnly.FromDateTime(since); day <= DateOnly.FromDateTime(now); day = day.AddDays(1))
        {
            result.Add(new DailyCount(day, counts.GetValueOrDefault(day, 0)));
        }
        return result;
    }

    private record DailyCount(DateOnly Date, int Count);
}
