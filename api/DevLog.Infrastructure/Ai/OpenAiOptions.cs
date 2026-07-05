namespace DevLog.Infrastructure.Ai;

// Bound from configuration section "OpenAi". One shared options class for
// both OpenAI use cases in this project (embeddings, always; generation,
// when Ai:GenerationProvider is set to "OpenAI") — deliberately consolidated
// rather than two separate config sections, since in practice there's only
// one OpenAI API key to manage either way.
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;

    // 1536 dimensions — must match Embedding.Vector's column type
    // (vector(1536)) exactly. Changing this model requires re-embedding
    // every existing row, not just new ones.
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    // Only used when Ai:GenerationProvider = "OpenAI" — gpt-4o-mini is the
    // cheap/fast tier, the same cost-tiering reasoning as Anthropic's
    // claude-haiku-4-5 choice for sanitization (Section 2's tiered-model
    // guidance), not open-ended/expensive generation.
    public string GenerationModel { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 2048;
}
