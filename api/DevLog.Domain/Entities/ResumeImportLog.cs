namespace DevLog.Domain.Entities;

// Milestone 13b — the cost-abuse control for resume import, same reasoning
// as SanitizationRunLog (Milestone 12): sending a resume through the LLM
// costs real money per call, and this is an authenticated action a creator
// could otherwise click repeatedly. Internal-only, same RLS shape as
// sanitization_run_logs — no public_read, no public_insert (this is never
// called anonymously, unlike portfolio_view_events/ai_query_logs).
public class ResumeImportLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
