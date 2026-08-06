using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Identity;

public class ClerkUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClerkUserService> _logger;

    public ClerkUserService(HttpClient httpClient, IOptions<ClerkOptions> options, ILogger<ClerkUserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
    }

    public async Task<string?> GetVerifiedGitHubUsernameAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"v1/users/{Uri.EscapeDataString(clerkUserId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Clerk Backend API GET /v1/users/{ClerkUserId} returned {StatusCode}. Check Clerk:SecretKey is set to a real secret key for this instance (not the placeholder), and that it hasn't been swapped for a different Clerk instance's key. Response: {Body}",
                clerkUserId, (int)response.StatusCode, body);
            return null;
        }

        var user = await response.Content.ReadFromJsonAsync<ClerkUserDto>(cancellationToken: cancellationToken);
        var githubAccount = user?.ExternalAccounts?.FirstOrDefault(a =>
            a.Provider == "oauth_github" &&
            string.Equals(a.Verification?.Status, "verified", StringComparison.OrdinalIgnoreCase));

        if (githubAccount is null)
        {
            _logger.LogInformation(
                "Clerk user {ClerkUserId} has no verified oauth_github external account yet (found {Count} external account(s) total).",
                clerkUserId, user?.ExternalAccounts?.Count ?? 0);
        }

        return string.IsNullOrWhiteSpace(githubAccount?.Username) ? null : githubAccount.Username;
    }

    private record ClerkUserDto([property: JsonPropertyName("external_accounts")] List<ClerkExternalAccountDto>? ExternalAccounts);

    private record ClerkExternalAccountDto(
        [property: JsonPropertyName("provider")] string? Provider,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("verification")] ClerkVerificationDto? Verification);

    private record ClerkVerificationDto([property: JsonPropertyName("status")] string? Status);
}
