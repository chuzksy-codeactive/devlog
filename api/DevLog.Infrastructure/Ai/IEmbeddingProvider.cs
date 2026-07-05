namespace DevLog.Infrastructure.Ai;

// A separate abstraction from ILlmProvider on purpose: Anthropic (the
// chosen generation provider) doesn't offer an embeddings endpoint, so
// embeddings come from a different vendor (OpenAI, decided 2026-07-05).
// Architecture doc Section 2 describes one LLM Gateway exposing embed()
// and generate() — this is that same idea, just split across two provider
// interfaces since one vendor can't do both. Swappable independently of
// ILlmProvider if either provider choice changes later.
public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
