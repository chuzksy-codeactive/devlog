namespace DevLog.Infrastructure.Ai;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public string GenerationModel { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 2048;
}
