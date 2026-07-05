namespace DevLog.Infrastructure.Ai;

// Bound from configuration section "Anthropic" (appsettings + Key Vault in
// production — see architecture doc Section 9's secrets management item).
// The API key must never be committed; see appsettings.Development.json.example.
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    // claude-haiku-4-5: fast + cheap, appropriate for a structured
    // find-and-replace-shaped task like sanitization rather than open-ended
    // generation — matches architecture doc Section 2's tiered-model
    // guidance (cheaper models where task complexity allows it).
    public string Model { get; set; } = "claude-haiku-4-5";

    public int MaxTokens { get; set; } = 2048;
}
