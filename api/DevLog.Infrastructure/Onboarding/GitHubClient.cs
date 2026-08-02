using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DevLog.Infrastructure.Onboarding;

// Milestone 13 — GitHub import, public-username-lookup shape (decided via
// AskUserQuestion over full OAuth: no token storage, no new Clerk
// integration, works for any developer regardless of what they've
// connected). Calls GitHub's public REST API unauthenticated — no
// Personal Access Token, no App registration. Trade-off accepted
// deliberately: GitHub's unauthenticated rate limit is 60 requests/hour
// per source IP, shared across every onboarding import this server makes.
// Fine at current (personal + early-onboarding) scale; if it becomes a
// real constraint, the fix is a single app-level PAT added to this
// client's default headers (same pattern as every other typed HttpClient
// in this codebase), not a per-user OAuth flow.
public class GitHubClient
{
    // Deliberately small: keeps the LLM prompt this feeds into a
    // reasonable size, and a candidate's most-starred/most-recently-pushed
    // non-fork repos are the ones actually worth suggesting content from.
    private const int MaxReposToConsider = 5;
    private const int MaxReposToFetchReadmesFor = 3;
    private const int ReadmeCharacterCap = 4000;

    private readonly HttpClient _httpClient;

    public GitHubClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(GitHubImportOutcome Outcome, GitHubImportData? Data)> FetchPublicProfileAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        using var reposResponse = await _httpClient.GetAsync(
            $"/users/{Uri.EscapeDataString(username)}/repos?type=owner&sort=pushed&per_page=30",
            cancellationToken);

        if (reposResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return (GitHubImportOutcome.UserNotFound, null);
        }

        // GitHub returns 403 (occasionally 429) with X-RateLimit-Remaining: 0
        // once the unauthenticated hourly cap is hit — not worth parsing the
        // header just to confirm what the status code + a 0-repos response
        // already tells us clearly enough for this feature.
        if (reposResponse.StatusCode == HttpStatusCode.Forbidden || (int)reposResponse.StatusCode == 429)
        {
            return (GitHubImportOutcome.RateLimited, null);
        }

        reposResponse.EnsureSuccessStatusCode();

        var allRepos = await reposResponse.Content.ReadFromJsonAsync<List<GitHubRepoDto>>(cancellationToken: cancellationToken)
            ?? new List<GitHubRepoDto>();

        var candidates = allRepos
            .Where(r => !r.Fork)
            .OrderByDescending(r => r.StargazersCount)
            .ThenByDescending(r => r.PushedAt)
            .Take(MaxReposToConsider)
            .ToList();

        if (candidates.Count == 0)
        {
            return (GitHubImportOutcome.NoPublicReposFound, null);
        }

        var readmes = new Dictionary<string, string>();
        foreach (var repo in candidates.Take(MaxReposToFetchReadmesFor))
        {
            var readme = await TryFetchReadmeAsync(username, repo.Name, cancellationToken);
            if (readme is not null)
            {
                readmes[repo.Name] = readme;
            }
        }

        var summaries = candidates
            .Select(r => new GitHubRepoSummary(
                r.Name,
                r.Description,
                r.Language,
                r.Topics ?? new List<string>(),
                r.StargazersCount,
                r.CreatedAt,
                r.PushedAt,
                r.HtmlUrl))
            .ToList();

        return (GitHubImportOutcome.Success, new GitHubImportData(summaries, readmes));
    }

    // The "raw" media type returns plain markdown text directly instead of
    // the default base64-wrapped-in-JSON shape — no manual decoding needed.
    // Best-effort: a repo with no README (or one GitHub can't render as
    // raw text) just means less context for that repo, not a failed import.
    private async Task<string?> TryFetchReadmeAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/readme");
        request.Headers.Add("Accept", "application/vnd.github.raw+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return text.Length > ReadmeCharacterCap ? text[..ReadmeCharacterCap] : text;
    }

    // Explicit JsonPropertyName throughout, even on fields that happen to
    // already be lowercase — same defensive convention as
    // AnthropicLlmProvider/OpenAiLlmProvider's request/response DTOs, since
    // System.Net.Http.Json's helpers don't apply case-insensitive matching
    // by default the way ASP.NET Core's own JSON options do.
    private record GitHubRepoDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("language")] string? Language,
        [property: JsonPropertyName("topics")] List<string>? Topics,
        [property: JsonPropertyName("stargazers_count")] int StargazersCount,
        [property: JsonPropertyName("fork")] bool Fork,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("pushed_at")] DateTime PushedAt,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}

public enum GitHubImportOutcome
{
    Success,
    UserNotFound,
    NoPublicReposFound,
    RateLimited
}

public record GitHubRepoSummary(
    string Name,
    string? Description,
    string? Language,
    List<string> Topics,
    int StargazersCount,
    DateTime CreatedAt,
    DateTime PushedAt,
    string HtmlUrl);

public record GitHubImportData(
    IReadOnlyList<GitHubRepoSummary> Repos,
    IReadOnlyDictionary<string, string> Readmes);
