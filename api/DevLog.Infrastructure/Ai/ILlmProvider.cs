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
}

// ModelVersion is persisted onto RawEntry.SanitizationModelVersion and later
// copied into SanitizationAudit.ModelVersion — the "model version" half of
// the audit trail architecture doc Section 3 requires for dispute defensibility.
public record SanitizationResult(string SanitizedContent, string ModelVersion);
