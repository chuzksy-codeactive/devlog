using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevLog.Domain.Entities;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevLog.Infrastructure.Onboarding;

public enum OnboardingSuggestionOutcome
{
    Suggested,
    GitHubUserNotFound,
    NoPublicReposFound,
    GitHubRateLimited,
    GitHubNotConnected,
    Error
}

public record SuggestedExperience(
    ExperienceType ExperienceType,
    string CompanyLabel,
    string Title,
    List<string> Technologies,
    string KeyAchievement,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? RepoUrl);

public record SuggestedEntry(string Title, string RawContent);

public record OnboardingSuggestions(List<SuggestedExperience> Experiences, SuggestedEntry? DraftEntry);

public record OnboardingSuggestionResult(OnboardingSuggestionOutcome Outcome, OnboardingSuggestions? Suggestions = null);

public enum ResumeImportOutcome
{
    Suggested,
    NoTextExtracted,
    RateLimited,
    MalwareDetected,
    ScanFailed,
    Error
}

public record ResumeImportResult(ResumeImportOutcome Outcome, string? CvFileUrl, OnboardingSuggestions? Suggestions = null);

public class OnboardingImportService
{
    private const int MaxResumeImportsPerTenantPerHour = 5;
    private const int MaxImportedAchievementBullets = 5;
    private const string ResumeFileName = "resume.pdf";

    private readonly GitHubClient _gitHubClient;
    private readonly ILlmProvider _llmProvider;
    private readonly DevLogDbContext _db;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMalwareScanner _malwareScanner;
    private readonly PromptInjectionTelemetry _promptInjectionTelemetry;
    private readonly ILogger<OnboardingImportService> _logger;

    public OnboardingImportService(
        GitHubClient gitHubClient,
        ILlmProvider llmProvider,
        DevLogDbContext db,
        IFileStorageProvider fileStorageProvider,
        IMalwareScanner malwareScanner,
        PromptInjectionTelemetry promptInjectionTelemetry,
        ILogger<OnboardingImportService> logger)
    {
        _gitHubClient = gitHubClient;
        _llmProvider = llmProvider;
        _db = db;
        _fileStorageProvider = fileStorageProvider;
        _malwareScanner = malwareScanner;
        _promptInjectionTelemetry = promptInjectionTelemetry;
        _logger = logger;
    }

    public async Task<OnboardingSuggestionResult> SuggestFromGitHubAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (portfolio is null || !portfolio.GitHubVerified || string.IsNullOrEmpty(portfolio.GitHubUrl))
        {
            return new OnboardingSuggestionResult(OnboardingSuggestionOutcome.GitHubNotConnected);
        }

        var username = portfolio.GitHubUrl.TrimEnd('/').Split('/')[^1];
        var (outcome, data) = await _gitHubClient.FetchPublicProfileAsync(username, cancellationToken);

        if (outcome != GitHubImportOutcome.Success || data is null)
        {
            return new OnboardingSuggestionResult(outcome switch
            {
                GitHubImportOutcome.UserNotFound => OnboardingSuggestionOutcome.GitHubUserNotFound,
                GitHubImportOutcome.NoPublicReposFound => OnboardingSuggestionOutcome.NoPublicReposFound,
                GitHubImportOutcome.RateLimited => OnboardingSuggestionOutcome.GitHubRateLimited,
                _ => OnboardingSuggestionOutcome.Error
            });
        }

        var context = BuildGitHubContext(data);

        _promptInjectionTelemetry.Scan(
            flow: "onboarding_github",
            inputType: "github_context",
            content: context,
            tenantId: tenantId);

        var rawJson = await _llmProvider.SuggestOnboardingContentAsync(context, anonymizeProjectNames: false, cancellationToken);

        var suggestions = TryParseGitHubSuggestions(rawJson, data);
        return suggestions is null
            ? new OnboardingSuggestionResult(OnboardingSuggestionOutcome.Error)
            : new OnboardingSuggestionResult(OnboardingSuggestionOutcome.Suggested, suggestions);
    }

    public async Task<ResumeImportResult> SuggestFromResumeAsync(
        Guid tenantId,
        Stream pdfContent,
        CancellationToken cancellationToken = default)
    {
        pdfContent.Position = 0;
        var cvFileUrl = await _fileStorageProvider.SaveAsync(tenantId, ResumeFileName, pdfContent, cancellationToken);

        var scanResult = await _malwareScanner.ScanStoredFileAsync(cvFileUrl, cancellationToken);
        if (scanResult.Outcome == MalwareScanOutcome.Infected)
        {
            await _fileStorageProvider.DeleteAsync(tenantId, ResumeFileName, cancellationToken);
            _logger.LogWarning("Resume upload rejected by malware scanner for tenant {TenantId}. Details: {Details}", tenantId, scanResult.Details);
            return new ResumeImportResult(ResumeImportOutcome.MalwareDetected, CvFileUrl: null);
        }

        if (scanResult.Outcome == MalwareScanOutcome.Failed)
        {
            await _fileStorageProvider.DeleteAsync(tenantId, ResumeFileName, cancellationToken);
            _logger.LogWarning("Resume upload scan failed for tenant {TenantId}. Details: {Details}", tenantId, scanResult.Details);
            return new ResumeImportResult(ResumeImportOutcome.ScanFailed, CvFileUrl: null);
        }

        _logger.LogInformation("Resume upload passed malware scanning for tenant {TenantId}.", tenantId);

        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (portfolio is not null)
        {
            portfolio.CvFileUrl = cvFileUrl;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var recentImportCount = await _db.ResumeImportLogs
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentImportCount >= MaxResumeImportsPerTenantPerHour)
        {
            return new ResumeImportResult(ResumeImportOutcome.RateLimited, cvFileUrl);
        }

        pdfContent.Position = 0;
        var resumeText = PdfTextExtractor.Extract(pdfContent);
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return new ResumeImportResult(ResumeImportOutcome.NoTextExtracted, cvFileUrl);
        }

        _promptInjectionTelemetry.Scan(
            flow: "onboarding_resume",
            inputType: "resume_text",
            content: resumeText,
            tenantId: tenantId);

        var rawJson = await _llmProvider.SuggestOnboardingContentFromResumeAsync(resumeText, anonymizeCompanyNames: false, cancellationToken);
        var suggestions = TryParseResumeSuggestions(rawJson);

        _db.ResumeImportLogs.Add(new ResumeImportLog { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);

        return suggestions is null
            ? new ResumeImportResult(ResumeImportOutcome.Error, cvFileUrl)
            : new ResumeImportResult(ResumeImportOutcome.Suggested, cvFileUrl, suggestions);
    }

    private static string BuildGitHubContext(GitHubImportData data)
    {
        var sb = new StringBuilder();
        foreach (var repo in data.Repos)
        {
            sb.AppendLine($"Repo: {repo.Name}");
            sb.AppendLine($"Description: {repo.Description ?? "(none)"}");
            sb.AppendLine($"Primary language: {repo.Language ?? "(unknown)"}");
            sb.AppendLine($"Topics: {(repo.Topics.Count > 0 ? string.Join(", ", repo.Topics) : "(none)")}");
            sb.AppendLine($"Stars: {repo.StargazersCount}");
            if (data.Readmes.TryGetValue(repo.Name, out var readme))
            {
                sb.AppendLine("README excerpt:");
                sb.AppendLine(readme);
            }
            sb.AppendLine("---");
        }
        return sb.ToString();
    }

    private static OnboardingSuggestions? TryParseGitHubSuggestions(string rawJson, GitHubImportData data)
    {
        try
        {
            var cleaned = StripMarkdownFences(rawJson);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<LlmSuggestionDto>(cleaned, options);
            if (dto is null)
            {
                return null;
            }

            var experiences = new List<SuggestedExperience>();
            var repos = data.Repos;
            var count = Math.Min(dto.Experiences?.Count ?? 0, repos.Count);

            for (var i = 0; i < count; i++)
            {
                var repo = repos[i];
                var suggestion = dto.Experiences![i];

                if (!Enum.TryParse<ExperienceType>(suggestion.ExperienceType, ignoreCase: true, out var experienceType))
                {
                    experienceType = ExperienceType.PersonalProject;
                }

                experiences.Add(new SuggestedExperience(
                    experienceType,
                    string.IsNullOrWhiteSpace(suggestion.CompanyLabel) ? repo.Name : suggestion.CompanyLabel,
                    suggestion.Title ?? string.Empty,
                    suggestion.Technologies ?? new List<string>(),
                    NormalizeImportedKeyAchievement(suggestion.KeyAchievement),
                    DateOnly.FromDateTime(repo.CreatedAt),
                    DateOnly.FromDateTime(repo.PushedAt),
                    repo.HtmlUrl));
            }

            var draftEntry = dto.DraftEntry is not null
                ? new SuggestedEntry(
                    string.IsNullOrWhiteSpace(dto.DraftEntry.Title) ? "Untitled draft" : dto.DraftEntry.Title!,
                    dto.DraftEntry.RawContent ?? string.Empty)
                : null;

            return new OnboardingSuggestions(experiences, draftEntry);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OnboardingSuggestions? TryParseResumeSuggestions(string rawJson)
    {
        try
        {
            var cleaned = StripMarkdownFences(rawJson);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<LlmResumeSuggestionDto>(cleaned, options);
            if (dto?.Experiences is null)
            {
                return null;
            }

            var experiences = new List<SuggestedExperience>();
            foreach (var suggestion in dto.Experiences)
            {
                if (!Enum.TryParse<ExperienceType>(suggestion.ExperienceType, ignoreCase: true, out var experienceType))
                {
                    experienceType = ExperienceType.Employer;
                }

                var startDate = TryParseYearMonth(suggestion.StartDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var endDate = TryParseYearMonth(suggestion.EndDate);

                experiences.Add(new SuggestedExperience(
                    experienceType,
                    string.IsNullOrWhiteSpace(suggestion.CompanyLabel) ? "A previous role" : suggestion.CompanyLabel,
                    suggestion.Title ?? string.Empty,
                    suggestion.Technologies ?? new List<string>(),
                    NormalizeImportedKeyAchievement(suggestion.KeyAchievement),
                    startDate,
                    endDate,
                    RepoUrl: null));
            }

            return new OnboardingSuggestions(experiences, DraftEntry: null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateOnly? TryParseYearMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM", out var yearMonth))
        {
            return yearMonth;
        }

        return DateOnly.TryParse(value, out var fallback) ? fallback : null;
    }

    private static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        trimmed = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed;
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    private static string NormalizeImportedKeyAchievement(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = rawText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var bullets = ExtractAchievementLines(normalized);
        if (bullets.Count == 0)
        {
            return string.Empty;
        }

        if (bullets.Count > MaxImportedAchievementBullets)
        {
            bullets = bullets.GetRange(0, MaxImportedAchievementBullets);
        }

        return string.Join("\n", bullets);
    }

    private static List<string> ExtractAchievementLines(string text)
    {
        var bullets = new List<string>();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeAchievementLine(line);
            if (normalized.Length > 0)
            {
                bullets.Add(normalized);
            }
        }

        if (bullets.Count > 0)
        {
            return bullets;
        }

        foreach (var sentence in Regex.Split(text, @"(?<=[.!?])\s+"))
        {
            var normalized = NormalizeAchievementLine(sentence);
            if (normalized.Length > 0)
            {
                bullets.Add(normalized);
            }
        }

        return bullets;
    }

    private static string NormalizeAchievementLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        trimmed = trimmed.TrimStart('-', '*', '•').Trim();
        return Regex.Replace(trimmed, @"^\d+[.)]\s*", string.Empty);
    }

    private record LlmSuggestionDto(List<LlmExperienceDto>? Experiences, LlmDraftEntryDto? DraftEntry);

    private record LlmExperienceDto(
        string? ExperienceType,
        string? CompanyLabel,
        string? Title,
        List<string>? Technologies,
        string? KeyAchievement);

    private record LlmDraftEntryDto(string? Title, string? RawContent);

    private record LlmResumeSuggestionDto(List<LlmResumeExperienceDto>? Experiences);

    private record LlmResumeExperienceDto(
        string? ExperienceType,
        string? CompanyLabel,
        string? Title,
        List<string>? Technologies,
        string? KeyAchievement,
        string? StartDate,
        string? EndDate);
}
