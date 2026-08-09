using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Ai;

public class OpenAiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiLlmProvider(HttpClient httpClient, IOptions<OpenAiOptions> options)
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

        var text = await CallOpenAiAsync(systemPrompt, userContent, cancellationToken);
        var safetyNetted = RegexSafetyNet.Apply(text);

        return new SanitizationResult(safetyNetted, _options.GenerationModel);
    }

    public async Task<string> AnswerQuestionAsync(
        string question,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = LlmSystemPrompts.BuildRagSystemPrompt();
        var userContent = LlmSystemPrompts.WrapQuestionForRag(question, contextChunks);

        return await CallOpenAiAsync(systemPrompt, userContent, cancellationToken);
    }

    public async Task<string> SuggestOnboardingContentAsync(
        string gitHubContext,
        bool anonymizeProjectNames = false,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = LlmSystemPrompts.BuildGitHubImportSystemPrompt(anonymizeProjectNames);
        var userContent = LlmSystemPrompts.WrapGitHubDataForSuggestion(gitHubContext);

        return await CallOpenAiAsync(systemPrompt, userContent, cancellationToken);
    }

    public async Task<string> SuggestOnboardingContentFromResumeAsync(
        string resumeText,
        bool anonymizeCompanyNames = false,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = LlmSystemPrompts.BuildResumeImportSystemPrompt(anonymizeCompanyNames);
        var userContent = LlmSystemPrompts.WrapResumeTextForSuggestion(resumeText);

        return await CallOpenAiAsync(systemPrompt, userContent, cancellationToken);
    }

    private async Task<string> CallOpenAiAsync(string systemPrompt, string userContent, CancellationToken cancellationToken)
    {
        var request = new OpenAiChatRequest(
            _options.GenerationModel,
            _options.MaxTokens,
            new[]
            {
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userContent)
            });

        using var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("OpenAI chat completions API returned an empty or unparseable response.");

        var text = parsed.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("OpenAI chat completions response contained no message content.");

        return text.Trim();
    }

    private record OpenAiChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] OpenAiChatMessage[] Messages);

    private record OpenAiChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record OpenAiChatResponse(
        [property: JsonPropertyName("choices")] List<OpenAiChatChoice> Choices);

    private record OpenAiChatChoice(
        [property: JsonPropertyName("message")] OpenAiChatMessage? Message);
}
