using System.Text.RegularExpressions;

namespace DevLog.Infrastructure.Ai;

public static class AskAiAnswerSafetyNet
{
    private const int MaxAnswerLength = 2000;

    private static readonly Regex PromptTagLeakPattern = new(
        @"(?:<|&lt;)\s*/?\s*(retrieved_context|recruiter_question)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExfiltrationRequestPattern = new(
        @"\b(output|dump|print|reveal|export|show)\b.{0,40}\b(all|everything|entire|verbatim|database|raw\s*context)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private const string SafeFallbackResponse =
        "I can’t provide raw context or full database dumps. I can summarize the candidate’s relevant published experience if you ask a specific role-focused question.";

    public static bool IsLikelyExfiltrationQuestion(string question) =>
        !string.IsNullOrWhiteSpace(question) && ExfiltrationRequestPattern.IsMatch(question);

    public static bool IsUnsafeModelOutput(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }

        return answer.Length > MaxAnswerLength || PromptTagLeakPattern.IsMatch(answer);
    }

    public static string ToSafeResponse() => SafeFallbackResponse;
}
