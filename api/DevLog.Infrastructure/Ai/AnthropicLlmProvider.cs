using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Ai;

// Anthropic Messages API implementation of the LLM Gateway. Registered as
// itself (not directly against ILlmProvider) in ServiceCollectionExtensions,
// with the Ai:GenerationProvider config switch deciding whether this or
// OpenAiLlmProvider actually gets resolved as ILlmProvider — see that file's
// comment for why (Chika couldn't get an Anthropic console API key working,
// 2026-07-05, so OpenAI needed to be a real swap-in, not just a documented
// possibility).
public class AnthropicLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;

    public AnthropicLlmProvider(HttpClient httpClient, IOptions<AnthropicOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SanitizationResult> SanitizeAsync(
        string rawContent,
        IReadOnlyList<string> blocklistTerms,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = LlmSystemPrompts.BuildSanitizationSystemPrompt(blocklistTerms);
        var userContent = LlmSystemPrompts.WrapEntryForSanitization(rawContent);

        var text = await CallAnthropicAsync(systemPrompt, userContent, cancellationToken);
        var safetyNetted = RegexSafetyNet.Apply(text);

        return new SanitizationResult(safetyNetted, _options.Model);
    }

    public async Task<string> AnswerQuestionAsync(
        string question,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = LlmSystemPrompts.BuildRagSystemPrompt();
        var userContent = LlmSystemPrompts.WrapQuestionForRag(question, contextChunks);

        return await CallAnthropicAsync(systemPrompt, userContent, cancellationToken);
    }

    private async Task<string> CallAnthropicAsync(string systemPrompt, string userContent, CancellationToken cancellationToken)
    {
        var request = new AnthropicRequest(
            _options.Model,
            _options.MaxTokens,
            systemPrompt,
            new[] { new AnthropicMessage("user", userContent) });

        using var response = await _httpClient.PostAsJsonAsync("/v1/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Anthropic API returned an empty or unparseable response.");

        var text = parsed.Content.FirstOrDefault(c => c.Type == "text")?.Text
            ?? throw new InvalidOperationException("Anthropic API response contained no text block.");

        return text.Trim();
    }

    private record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] AnthropicMessage[] Messages);

    private record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContentBlock> Content);

    private record AnthropicContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
