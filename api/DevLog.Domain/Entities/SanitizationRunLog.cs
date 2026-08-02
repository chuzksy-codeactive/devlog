namespace DevLog.Domain.Entities;

// Milestone 12 — closes the other Phase 1 gap the roadmap flagged: "a
// per-tenant rate cap on sanitization runs, separate from the monthly
// quota, since that's an LLM-cost abuse vector." Subscription.SanitizationQuota
// existed since early in the build but nothing ever counted against it.
// Logs every call to SanitizationPipelineService.SanitizeAsync, not just
// approved entries — re-running sanitize on a Draft/Rejected entry is a
// real LLM call and a real cost each time, whether or not the creator ever
// approves the result. Backs two independent checks off the same table:
// a short rolling-window count (the abuse-prevention rate cap, same shape
// as AiQueryLog's hourly cap) and a calendar-month count (the actual
// SanitizationQuota enforcement).
public class SanitizationRunLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
