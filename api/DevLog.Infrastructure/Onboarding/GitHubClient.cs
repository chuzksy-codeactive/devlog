using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DevLog.Infrastructure.Onboarding;

public class GitHubClient
{
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
