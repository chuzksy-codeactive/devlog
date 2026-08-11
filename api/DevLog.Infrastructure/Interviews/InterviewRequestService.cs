using System.Net;
using System.Security.Cryptography;
using DevLog.Domain.Entities;
using DevLog.Infrastructure.Email;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Interviews;

public enum RequestInterviewOutcome
{
    Created,
    PortfolioNotFound,
    EmailProviderPendingReview,
    EmailDeliveryFailed
}

public record RequestInterviewResult(RequestInterviewOutcome Outcome, Guid? Id = null);

public enum VerifyEmailOutcome
{
    Verified,
    AlreadyVerified,
    InvalidOrExpired,
    NotFound
}

public class InterviewRequestService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly DevLogDbContext _db;
    private readonly IEmailProvider _emailProvider;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<InterviewRequestService> _logger;

    public InterviewRequestService(
        DevLogDbContext db,
        IEmailProvider emailProvider,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<InterviewRequestService> logger)
    {
        _db = db;
        _emailProvider = emailProvider;
        _tenantAccessor = tenantAccessor;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<RequestInterviewResult> CreateAsync(
        string slug,
        string recruiterEmail,
        string? recruiterName,
        string note,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Tenant!.Slug == slug, cancellationToken);

        if (portfolio is null || portfolio.Visibility == PortfolioVisibility.Paused)
        {
            return new RequestInterviewResult(RequestInterviewOutcome.PortfolioNotFound);
        }

        var verificationToken = RandomNumberGenerator.GetHexString(64);

        var request = new InterviewRequest
        {
            Id = Guid.NewGuid(),
            TenantId = portfolio.TenantId,
            RecruiterEmail = recruiterEmail,
            RecruiterName = recruiterName,
            Note = note,
            Status = InterviewRequestStatus.Pending,
            EmailVerified = false,
            VerificationToken = HashVerificationToken(verificationToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime),
            CreatedAt = DateTime.UtcNow
        };

        _db.InterviewRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendVerificationEmailAsync(slug, request, verificationToken, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogWarning(
                ex,
                "Postmark returned 422 for interview request {RequestId}; removing unsent request",
                request.Id);
            await RemoveInterviewRequestSafelyAsync(request, cancellationToken);
            return new RequestInterviewResult(RequestInterviewOutcome.EmailProviderPendingReview);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send verification email for interview request {RequestId}; removing unsent request",
                request.Id);
            await RemoveInterviewRequestSafelyAsync(request, cancellationToken);
            return new RequestInterviewResult(RequestInterviewOutcome.EmailDeliveryFailed);
        }

        return new RequestInterviewResult(RequestInterviewOutcome.Created, request.Id);
    }

    private async Task RemoveInterviewRequestSafelyAsync(InterviewRequest request, CancellationToken cancellationToken)
    {
        _db.InterviewRequests.Remove(request);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(
                ex,
                "Interview request {RequestId} was already removed during cleanup",
                request.Id);
        }
    }

    private async Task SendVerificationEmailAsync(
        string slug,
        InterviewRequest request,
        string verificationToken,
        CancellationToken cancellationToken)
    {
        var verifyUrl =
            $"{_frontendOptions.BaseUrl.TrimEnd('/')}/verify-interview-request?slug={Uri.EscapeDataString(slug)}&requestId={request.Id}&token={verificationToken}";

        await _emailProvider.SendAsync(
            request.RecruiterEmail,
            "Confirm your DevLog interview request",
            "Click the link below to confirm this is your email address and send your interview request:\n\n" +
            $"{verifyUrl}\n\n" +
            "This link expires in 24 hours. If you didn't request this, you can ignore this email.",
            cancellationToken);
    }

    public async Task<VerifyEmailOutcome> VerifyEmailAsync(
        string slug,
        Guid requestId,
        string token,
        CancellationToken cancellationToken = default)
    {
        await SetTenantContextFromSlugAsync(slug, cancellationToken);

        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return VerifyEmailOutcome.NotFound;
        }

        if (request.EmailVerified)
        {
            return VerifyEmailOutcome.AlreadyVerified;
        }

        if (request.VerificationToken is null
            || !MatchesStoredVerificationToken(request.VerificationToken, token)
            || request.VerificationTokenExpiresAt is not DateTime expiresAt
            || expiresAt < DateTime.UtcNow)
        {
            return VerifyEmailOutcome.InvalidOrExpired;
        }

        request.EmailVerified = true;
        request.VerificationToken = null;
        request.VerificationTokenExpiresAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.TenantId == request.TenantId, cancellationToken);
        if (portfolio is not null)
        {
            await NotifyCreatorAsync(portfolio, request, cancellationToken);
        }

        return VerifyEmailOutcome.Verified;
    }

    private async Task NotifyCreatorAsync(Portfolio portfolio, InterviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portfolio.NotificationEmail))
        {
            return;
        }

        try
        {
            var fromLine = string.IsNullOrWhiteSpace(request.RecruiterName)
                ? request.RecruiterEmail
                : $"{request.RecruiterName} ({request.RecruiterEmail})";

            await _emailProvider.SendAsync(
                portfolio.NotificationEmail,
                "New interview request on DevLog",
                $"{fromLine} sent you an interview request:\n\n\"{request.Note}\"\n\n" +
                "Review, accept, or decline it via GET/POST /api/interview-requests.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send interview request notification email for request {RequestId}",
                request.Id);
        }
    }

    public async Task<InterviewRequest?> AcceptAsync(
        Guid id,
        string contactInstructions,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        request.Status = InterviewRequestStatus.Accepted;
        request.ContactInstructions = contactInstructions;
        request.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return request;
    }

    private async Task SetTenantContextFromSlugAsync(string slug, CancellationToken cancellationToken)
    {
        if (_tenantAccessor is not CurrentTenantAccessor accessor || accessor.TenantId is not null)
        {
            return;
        }

        var tenantId = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId is Guid resolvedTenantId)
        {
            accessor.TenantId = resolvedTenantId;
        }
    }

    public async Task<InterviewRequest?> DeclineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _db.InterviewRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        request.Status = InterviewRequestStatus.Declined;
        request.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return request;
    }

    private static string HashVerificationToken(string token)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static bool MatchesStoredVerificationToken(string storedToken, string presentedToken)
    {
        var presentedHash = HashVerificationToken(presentedToken);

        if (string.Equals(storedToken, presentedToken, StringComparison.Ordinal))
        {
            return true;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(storedToken),
            System.Text.Encoding.UTF8.GetBytes(presentedHash));
    }
}
