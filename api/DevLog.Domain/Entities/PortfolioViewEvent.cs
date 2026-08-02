namespace DevLog.Domain.Entities;

// Milestone 12 — closes the Phase 1 gap the roadmap flagged and this build
// never actually built: "instrument everything... portfolio_view_events...
// populated from day one, even though the creator-facing analytics *page*
// is a Phase 2 item." No UI reads this table yet (that's Milestone 16's
// creator analytics dashboard) — this just starts the logging now, per the
// roadmap's own "cheap to add alongside each feature as it's built" framing.
// Same shape as AiQueryLog: a coarse, non-identifying viewer signal rather
// than a real visitor identity, since recruiters never log in in Phase 1/2.
public class PortfolioViewEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    // A hashed IP, same construction as AskAiController.HashRemoteIp — good
    // enough to distinguish "many different viewers" from "one viewer
    // refreshing," without storing a raw IP address.
    public string? ViewerHash { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
