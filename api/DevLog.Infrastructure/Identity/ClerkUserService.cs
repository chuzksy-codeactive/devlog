using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Identity;

// Milestone 19 — the trust boundary for GitHub-link verification. Chika
// flagged that nothing confirmed a creator actually owned the GitHub
// account they typed into their profile (or the username they passed to
// the Milestone 13 GitHub-import endpoint) — decided via AskUserQuestion to
// close this with real OAuth rather than a lighter-weight ownership
// challenge. Deliberately calls Clerk's own Backend API server-to-server
// (using Clerk:SecretKey, never exposed to the browser) rather than trusting
// whatever the frontend reports about the signed-in user's linked accounts —
// a client-submitted "I'm verified, trust me" claim would be exactly as
// spoofable as the free-text field this replaces. The frontend's job is
// only to drive Clerk's own createExternalAccount() OAuth flow (see
// dashboard/profile/page.tsx) and then call POST /api/portfolio/me/github/sync
// so this service can independently confirm what Clerk actually recorded.
public class ClerkUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClerkUserService> _logger;

    public ClerkUserService(HttpClient httpClient, IOptions<ClerkOptions> options, ILogger<ClerkUserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // Only the token needs to change per call, everything else is
        // static — set once here rather than per-request, same as every
        // other typed HttpClient in this codebase (ServiceCollectionExtensions
        // sets Authorization at registration time for the others; this one
        // has nothing else that varies so it's set in the constructor
        // instead, functionally identical).
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
    }

    // Returns the verified GitHub username, or null if this Clerk user has
    // no GitHub external account, or has one that Clerk hasn't itself
    // marked "verified" (e.g. a stalled/incomplete OAuth flow). "Not
    // connected" (a real, common state) and "the Clerk API call itself
    // failed" (a real bug — bad Clerk:SecretKey, wrong instance, network
    // issue) both used to collapse into the same silent null return here,
    // which is exactly the "an RLS insert failure went silent" shape
    // Milestone 12 already got burned by once with portfolio_view_events/
    // ai_query_logs — logged explicitly now so a misconfigured secret key
    // shows up in the API logs instead of just quietly never verifying
    // anyone.
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
