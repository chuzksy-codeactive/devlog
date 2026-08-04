using System.Text;
using System.Text.Json;
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
    // Milestone 19 — the tenant hasn't connected (or Clerk hasn't verified)
    // a GitHub account yet. Distinct from GitHubUserNotFound: that outcome
    // means GitHub itself couldn't find the account; this one means DevLog
    // never got as far as asking GitHub, because there's no verified
    // account on file to ask about.
    GitHubNotConnected,
    Error
}

// StartDate/EndDate come from the repo's own created_at/pushed_at, not the
// LLM — GitHub's own timestamps are exact; asking the model to guess dates
// would just be a worse version of data already available. EndDate =
// pushed_at is a deliberate default, not "this project is finished" — it's
// "here's when the developer was last actively working on it," a
// reasonable draft the creator can blank out to "present" if it's still
// actively maintained; nothing here saves without the creator reviewing it
// first regardless. RepoUrl is null for resume-sourced suggestions (Milestone
// 13b) — there's no repo to link to.
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

// CvFileUrl is always populated once storage succeeds, even on a
// NoTextExtracted/RateLimited/Error outcome for the AI half — the resume
// file itself is real and worth hosting regardless of whether suggestions
// could be drafted from it this time.
public record ResumeImportResult(ResumeImportOutcome Outcome, string? CvFileUrl, OnboardingSuggestions? Suggestions = null);

// Orchestrates Milestone 13's GitHub import and Milestone 13b's resume
// import: fetch/extract source data, ask the LLM to draft suggestions,
// parse the result into the same shapes ExperienceController/EntriesController
// already accept. The GitHub half stays fully stateless/read-only. The
// resume half necessarily touches the database (storing the file, updating
// Portfolio.CvFileUrl, logging against the rate cap) but still never
// creates Experience rows itself — the creator reviews suggestions and
// saves them (or edits first) through the normal endpoints, same as
// GitHub import.
public class OnboardingImportService
{
    // Mirrors SanitizationPipelineService's MaxRunsPerTenantPerHour shape —
    // an authenticated creator action that costs a real LLM call, capped
    // lower than Ask AI's anonymous-facing limit since this is a
    // deliberate, infrequent self-serve action, not something a stranger
    // hits repeatedly.
    private const int MaxResumeImportsPerTenantPerHour = 5;
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

    // Milestone 19 — takes tenantId instead of a client-submitted username.
    // Previously this trusted whatever username the frontend sent, which
    // meant anyone could import a stranger's public repos into their own
    // onboarding drafts. Now the only username this will ever fetch from
    // GitHub is the one Clerk has independently verified belongs to the
    // signed-in creator (PortfolioController's me/github/sync endpoint is
    // the only place GitHubVerified/GitHubUrl get set together) — same
    // "look it up server-side from the authenticated tenant, don't trust a
    // client-submitted value for anything that grants access" posture as
    // every other tenant-scoped lookup in this codebase.
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

        var rawJson = await _llmProvider.SuggestOnboardingContentAsync(context, cancellationToken);

        var suggestions = TryParseGitHubSuggestions(rawJson, data);
        return suggestions is null
            ? new OnboardingSuggestionResult(OnboardingSuggestionOutcome.Error)
            : new OnboardingSuggestionResult(OnboardingSuggestionOutcome.Suggested, suggestions);
    }

    // Milestone 13b. pdfContent must be a seekable, positioned-at-0 stream —
    // read twice (once verbatim for storage, once via PdfPig for text
    // extraction), which is why OnboardingController buffers the upload
    // into a MemoryStream before calling this rather than passing the raw
    // request stream straight through.
    public async Task<ResumeImportResult> SuggestFromResumeAsync(
        Guid tenantId,
        Stream pdfContent,
        CancellationToken cancellationToken = default)
    {
        // Always store first — the creator explicitly chose to upload and
        // host this resume; that succeeds independently of whether the AI
        // extraction below finds anything usable (e.g. a scanned/image PDF
        // still deserves to be linked from the public portfolio, even
        // though PdfPig can't read text out of it).
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

        var rawJson = await _llmProvider.SuggestOnboardingContentFromResumeAsync(resumeText, cancellationToken);
        var suggestions = TryParseResumeSuggestions(rawJson);

        // Logged after the LLM call regardless of whether parsing
        // succeeded — a real call (and a real cost) happened either way,
        // so it counts against the rate cap either way.
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

    // Matches suggestions back to repos by list position (the prompt
    // explicitly asks for "exactly one experiences entry per repo, in the
    // same order") rather than by name, so a model that slightly reformats
    // a repo name in its output still lines up correctly with the repo's
    // own real StartDate/EndDate/RepoUrl.
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
                    suggestion.KeyAchievement ?? string.Empty,
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
            // The model returned something that isn't valid JSON matching
            // the requested shape — surfaced to the caller as a generic
            // Error outcome (429/404-style specificity doesn't apply here;
            // there's nothing more actionable to tell the creator than
            // "try again").
            return null;
        }
    }

    // Unlike the GitHub parser, there's no external list to align results
    // against by position — every suggested experience comes straight from
    // the LLM's own read of the resume, dates included (StartDate/EndDate
    // here are the one place in this file the LLM's own guesses are used
    // directly, since a resume's dates only exist in the text itself,
    // unlike a GitHub repo's created_at/pushed_at).
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
                    suggestion.KeyAchievement ?? string.Empty,
                    startDate,
                    endDate,
                    RepoUrl: null));
            }

            // Resumes don't naturally produce Sprint-Journal-shaped draft
            // content the way a GitHub README does — no draftEntry field
            // is requested in the resume prompt at all.
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
