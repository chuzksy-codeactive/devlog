using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Ai;

public class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiEmbeddingProvider(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var request = new OpenAiEmbeddingRequest(_options.EmbeddingModel, texts.ToArray());

        using var response = await _httpClient.PostAsJsonAsync("/v1/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("OpenAI embeddings API returned an empty or unparseable response.");

        // OpenAI's response items aren't guaranteed to preserve input order
        // in every client library, but the raw API does return them in
        // request order with an `index` field — sort on it explicitly
        // rather than assuming array order, since a chunk landing in the
        // wrong embedding slot would silently corrupt retrieval.
        return parsed.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private record OpenAiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input);

    private record OpenAiEmbeddingResponse(
        [property: JsonPropertyName("data")] List<OpenAiEmbeddingData> Data);

    private record OpenAiEmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
