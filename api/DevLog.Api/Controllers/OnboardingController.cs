using DevLog.Infrastructure.MultiTenancy;
using DevLog.Infrastructure.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevLog.Api.Controllers;

// Milestone 13 — guided onboarding. Creator-authenticated (matches the
// roadmap's "guided onboarding" being a creator-side feature). The GitHub
// import half doesn't touch ICurrentTenantAccessor.TenantId at all — it
// only ever generates suggestions, it never persists anything itself.
// Saving a suggestion is just a normal POST /api/experiences or POST
// /api/entries call from the frontend once the creator has reviewed it.
// Milestone 13b's resume import DOES need the tenant id — unlike GitHub
// import, it writes (the uploaded file, Portfolio.CvFileUrl, the rate-limit
// log row) as an unavoidable side effect of "store the file regardless of
// AI-extraction success," not just to generate suggestions.
[ApiController]
[Route("api/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    // 10 MB — generous for a text-based resume PDF (even a few embedded
    // images rarely push a resume past a couple MB) while still bounding
    // worst-case memory use, since SuggestFromResumeAsync buffers the
    // whole file into memory to read it twice (store, then extract).
    private const long MaxResumeFileSizeBytes = 10 * 1024 * 1024;

    private readonly OnboardingImportService _importService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public OnboardingController(OnboardingImportService importService, ICurrentTenantAccessor tenantAccessor)
    {
        _importService = importService;
        _tenantAccessor = tenantAccessor;
    }

    // Milestone 19 — no longer takes a request body. Previously accepted
    // any client-submitted username with zero ownership check (anyone could
    // import a stranger's public repos as their own onboarding content);
    // now it only ever pulls from the tenant's own Clerk-verified GitHub
    // account, resolved server-side by OnboardingImportService from
    // ICurrentTenantAccessor — see that method's doc comment.
    [HttpPost("github-import")]
    public async Task<IActionResult> GitHubImport(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        var result = await _importService.SuggestFromGitHubAsync(tenantId, cancellationToken);

        return result.Outcome switch
        {
            OnboardingSuggestionOutcome.Suggested => Ok(result.Suggestions),
            OnboardingSuggestionOutcome.GitHubNotConnected =>
                BadRequest("Connect your GitHub account in your profile before importing."),
            OnboardingSuggestionOutcome.GitHubUserNotFound =>
                NotFound("Your connected GitHub account could not be found."),
            OnboardingSuggestionOutcome.NoPublicReposFound =>
                NotFound("No public, non-fork repositories found for your connected GitHub account."),
            OnboardingSuggestionOutcome.GitHubRateLimited =>
                StatusCode(StatusCodes.Status429TooManyRequests, "GitHub's rate limit was reached. Try again in a few minutes."),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "Could not generate suggestions from this GitHub profile.")
        };
    }

    // PDF magic bytes ("%PDF-"). A user could rename any file to .pdf, and
    // browsers don't reliably send an honest Content-Type for uploads —
    // checking the actual bytes is the only check worth trusting here.
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    [HttpPost("resume-import")]
    [RequestSizeLimit(MaxResumeFileSizeBytes)]
    public async Task<IActionResult> ResumeImport(IFormFile? file, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId)
        {
            return Unauthorized("No tenant context resolved from the current token.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("A resume file is required.");
        }

        if (file.Length > MaxResumeFileSizeBytes)
        {
            return BadRequest($"Resume file exceeds the {MaxResumeFileSizeBytes / (1024 * 1024)}MB limit.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF resumes are supported.");
        }

        // Buffered once here (not passed as the raw request stream) so it
        // can be read twice downstream: SuggestFromResumeAsync stores the
        // exact bytes via IFileStorageProvider, then rewinds and reads
        // again for PdfTextExtractor.
        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        if (!LooksLikePdf(buffer))
        {
            return BadRequest("This file does not look like a valid PDF.");
        }
        buffer.Position = 0;

        var result = await _importService.SuggestFromResumeAsync(tenantId, buffer, cancellationToken);

        return result.Outcome switch
        {
            ResumeImportOutcome.Suggested => Ok(new { cvFileUrl = result.CvFileUrl, suggestions = result.Suggestions }),
            ResumeImportOutcome.NoTextExtracted => Ok(new
            {
                cvFileUrl = result.CvFileUrl,
                suggestions = (OnboardingSuggestions?)null,
                message = "Resume was saved, but no text could be extracted (likely a scanned/image-only PDF). Add experience manually."
            }),
            ResumeImportOutcome.RateLimited =>
                StatusCode(StatusCodes.Status429TooManyRequests, "Resume import limit reached. Try again in an hour."),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "Could not generate suggestions from this resume.")
        };
    }

    private static bool LooksLikePdf(Stream stream)
    {
        if (stream.Length < PdfMagicBytes.Length)
        {
            return false;
        }

        var header = new byte[PdfMagicBytes.Length];
        var read = stream.Read(header, 0, header.Length);
        stream.Position = 0;
        return read == header.Length && header.SequenceEqual(PdfMagicBytes);
    }
}
