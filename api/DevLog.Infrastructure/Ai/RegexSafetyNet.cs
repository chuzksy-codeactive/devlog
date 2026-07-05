using System.Text.RegularExpressions;

namespace DevLog.Infrastructure.Ai;

// The third leg of architecture doc Section 3's sanitization technique:
// "Regex safety net — catches structured leaks the LLM pass might miss:
// email addresses, internal hostnames/URLs, ticket ID formats." Runs on the
// LLM's output, after the NER pass, as a deterministic backstop — not a
// replacement for it.
public static class RegexSafetyNet
{
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    // Matches things like JIRA/Linear-style ticket IDs: ABC-1234, PROJ-99.
    private static readonly Regex TicketIdPattern = new(
        @"\b[A-Z]{2,10}-\d{2,6}\b",
        RegexOptions.Compiled);

    // Bare hostnames/URLs pointing at internal-looking infra
    // (*.internal, *.corp, *.local) rather than public domains generically —
    // deliberately narrow to avoid stripping legitimate public tech mentions
    // ("deployed to AWS", "used github.com").
    private static readonly Regex InternalHostPattern = new(
        @"\b(?:[a-zA-Z0-9-]+\.)+(?:internal|corp|local|intranet)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Apply(string sanitizedContent)
    {
        var result = EmailPattern.Replace(sanitizedContent, "[redacted email]");
        result = TicketIdPattern.Replace(result, "[redacted ticket id]");
        result = InternalHostPattern.Replace(result, "[redacted internal host]");
        return result;
    }
}
