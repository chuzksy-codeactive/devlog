namespace DevLog.Infrastructure;

// Bound from configuration section "Frontend". Currently just the one
// value the backend needs to build a link a recruiter clicks from their
// email client (InterviewRequestService's verification email,
// Milestone 10) — grows if a second use case for "know the frontend's own
// URL" shows up.
public class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; set; } = "http://localhost:3000";
}
