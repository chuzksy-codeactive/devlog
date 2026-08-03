using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Api.Controllers;

// Milestone 16 — creator analytics dashboard. Purely a reader: every table
// this queries has been logging since well before this milestone
// (portfolio_view_events and ai_query_logs since Milestone 12,
// interview_requests since Milestone 5) — the roadmap deliberately started
// instrumentation early with "cheap to add alongside each feature as it's
// built... UI follows once there's enough to show." This controller is that
// UI's first (and so far only) consumer; it writes nothing.
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

    // GET /api/analytics/summary — everything the dashboard page needs in
    // one call: lifetime + 7d/30d totals for views and Ask AI queries,
    // interview request counts by state, and a zero-filled daily series for
    // the last 30 days (views + queries) to drive a simple bar chart.
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

        // Pulled as raw timestamps rather than aggregated with GROUP BY —
        // day-bucketing (including zero-filling days with no events) is far
        // simpler to express in C# than a portable DATE_TRUNC query, and at
        // this scale (one tenant's 30-day window) there's no real cost to
        // doing it in memory instead of in Postgres.
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
            // Verified is the meaningful denominator here, not the raw
            // total — an unverified request never reaches the creator's
            // inbox at all (Milestone 10), so counting it alongside real
            // ones would overstate actual recruiter interest.
            interviewRequestsVerified = interviewRequests.Count(r => r.EmailVerified),
            interviewRequestsPending = interviewRequests.Count(r => r.EmailVerified && r.Status == InterviewRequestStatus.Pending),
            interviewRequestsAccepted = interviewRequests.Count(r => r.EmailVerified && r.Status == InterviewRequestStatus.Accepted),
            viewsByDay = BucketByDay(viewTimestamps, since30d, now),
            queriesByDay = BucketByDay(queryTimestamps, since30d, now)
        });
    }

    // Fills every day in [since, now] with a count, including zero-activity
    // days — a chart with gaps for silent days reads as "broken," not "no
    // activity that day."
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
