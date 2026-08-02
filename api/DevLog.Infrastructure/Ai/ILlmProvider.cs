namespace DevLog.Infrastructure.Ai;

// The internal LLM Gateway abstraction referenced by architecture doc
// Section 5's stack table: "Internal ILlmProvider abstraction over
// HttpClientFactory — single interface enables the tiered cost strategy
// and provider swap without pipeline rewrites." Anthropic is the only
// implementation for now (2026-07-05 decision); OpenAI/Groq/Together stay
// swappable behind this same interface if pricing or quality demands it.
public interface ILlmProvider
{
    Task<SanitizationResult> SanitizeAsync(
        string rawContent,
        IReadOnlyList<string> blocklistTerms,
        CancellationToken cancellationToken = default);

    // The "generate()" half of the RAG pipeline (Section 2) — answers a
    // recruiter's question using only the retrieved chunks passed in, never
    // the model's own general knowledge about the candidate (there is none;
    // this guards against confident-sounding hallucination instead).
    Task<string> AnswerQuestionAsync(
        string question,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken = default);

    // Milestone 13 — the AI-extraction half of guided onboarding. Takes a
    // flattened summary of a developer's public GitHub repos (built by
    // OnboardingImportService) and returns raw JSON text (structured
    // Experience suggestions + one draft Sprint Journal entry) for the
    // caller to parse — nothing here persists anything or bypasses the
    // sanitize-and-approve pipeline; it only drafts what a human still has
    // to review and explicitly save.
    Task<string> SuggestOnboardingContentAsync(
        string gitHubContext,
        CancellationToken cancellationToken = default);

    // Milestone 13b — the resume half of guided onboarding. Unlike GitHub
    // repo data (already public, safe to reference verbatim), resume text
    // is exactly the kind of raw employer-identifying content the
    // anonymization pipeline exists to strip — but Experience entries
    // never pass through SanitizationPipelineService (see Experience.cs's
    // "CompanyLabel is free text the creator types themselves" design).
    // This prompt is the one place company names get anonymized as part
    // of extraction itself, applying the same generic-substitute rules as
    // BuildSanitizationSystemPrompt, rather than skipping that step
    // because there's no separate sanitize pass to catch it.
    Task<string> SuggestOnboardingContentFromResumeAsync(
        string resumeText,
        CancellationToken cancellationToken = default);
}

// ModelVersion is persisted onto RawEntry.SanitizationModelVersion and later
// copied into SanitizationAudit.ModelVersion — the "model version" half of
// the audit trail architecture doc Section 3 requires for dispute defensibility.
public record SanitizationResult(string SanitizedContent, string ModelVersion);
