using DevLog.Infrastructure.MultiTenancy;
using DevLog.Infrastructure.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevLog.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private const long MaxResumeFileSizeBytes = 3 * 1024 * 1024;

    private static readonly string[] AllowedPdfContentTypes =
    {
        "application/pdf",
        "application/x-pdf"
    };

    private readonly OnboardingImportService _importService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public OnboardingController(OnboardingImportService importService, ICurrentTenantAccessor tenantAccessor)
    {
        _importService = importService;
        _tenantAccessor = tenantAccessor;
    }

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

        if (!AllowedPdfContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF resumes are supported.");
        }

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
            ResumeImportOutcome.MalwareDetected =>
                BadRequest("This file failed malware scanning and was rejected."),
            ResumeImportOutcome.ScanFailed =>
                StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Resume scanning is temporarily unavailable. Try again shortly."),
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
