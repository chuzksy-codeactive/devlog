using System.Text.RegularExpressions;

namespace DevLog.Infrastructure.Ai;

public static class RegexSafetyNet
{
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex TicketIdPattern = new(
        @"\b[A-Z]{2,10}-\d{2,6}\b",
        RegexOptions.Compiled);

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
