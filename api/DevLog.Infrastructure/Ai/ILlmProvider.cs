namespace DevLog.Infrastructure.Ai;

public interface ILlmProvider
{
    Task<SanitizationResult> SanitizeAsync(
        string rawContent,
        IReadOnlyList<string> blocklistTerms,
        CancellationToken cancellationToken = default);

    Task<string> AnswerQuestionAsync(
        string question,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken = default);

    Task<string> SuggestOnboardingContentAsync(
        string gitHubContext,
        CancellationToken cancellationToken = default);

    Task<string> SuggestOnboardingContentFromResumeAsync(
        string resumeText,
        CancellationToken cancellationToken = default);
}

public record SanitizationResult(string SanitizedContent, string ModelVersion);
