using System.Text.RegularExpressions;
using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Email;
using DevLog.Infrastructure.Interviews;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace DevLog.Tests;

public class InterviewRequestServiceTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=devlog;Username=devlog_app;******";
    private static readonly NpgsqlDataSource DataSource = BuildDataSource();

    [Fact]
    public async Task CreateAsync_WhenEmailSendFails_ReturnsEmailDeliveryFailedAndRemovesRequest()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        await using var db = CreateDbContext();
        var slug = $"alice-{Guid.NewGuid():N}";
        try
        {
            SeedPortfolio(db, slug);

            var service = new InterviewRequestService(
                db,
                new ThrowingEmailProvider(),
                new CurrentTenantAccessor(),
                Options.Create(new FrontendOptions { BaseUrl = "http://localhost:3000" }),
                NullLogger<InterviewRequestService>.Instance);

            var result = await service.CreateAsync(slug, "recruiter@example.com", "Recruiter", "Let's talk.");

            Assert.Equal(RequestInterviewOutcome.EmailDeliveryFailed, result.Outcome);
            Assert.Equal(0, await db.InterviewRequests.CountAsync());
        }
        finally
        {
            await CleanupSeedDataAsync(db, slug);
        }
    }

    [Fact]
    public async Task CreateAsync_SendsRawVerificationTokenThatCanBeVerified()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        await using var db = CreateDbContext();
        var slug = $"bob-{Guid.NewGuid():N}";
        try
        {
            SeedPortfolio(db, slug);
            var emailProvider = new CapturingEmailProvider();

            var service = new InterviewRequestService(
                db,
                emailProvider,
                new CurrentTenantAccessor(),
                Options.Create(new FrontendOptions { BaseUrl = "http://localhost:3000" }),
                NullLogger<InterviewRequestService>.Instance);

            var result = await service.CreateAsync(slug, "recruiter@example.com", null, "Interested in your profile.");

            Assert.Equal(RequestInterviewOutcome.Created, result.Outcome);
            Assert.NotNull(result.Id);
            Assert.NotNull(emailProvider.LastBody);

            var token = ExtractToken(emailProvider.LastBody!);
            var request = await db.InterviewRequests.SingleAsync(r => r.Id == result.Id);

            Assert.NotEqual(request.VerificationToken, token);

            var verifyOutcome = await service.VerifyEmailAsync(slug, result.Id!.Value, token);
            Assert.Equal(VerifyEmailOutcome.Verified, verifyOutcome);
        }
        finally
        {
            await CleanupSeedDataAsync(db, slug);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenEmailProviderReturns422_ReturnsProviderPendingReviewAndRemovesRequest()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        await using var db = CreateDbContext();
        var slug = $"carol-{Guid.NewGuid():N}";
        try
        {
            SeedPortfolio(db, slug);

            var service = new InterviewRequestService(
                db,
                new Throwing422EmailProvider(),
                new CurrentTenantAccessor(),
                Options.Create(new FrontendOptions { BaseUrl = "http://localhost:3000" }),
                NullLogger<InterviewRequestService>.Instance);

            var result = await service.CreateAsync(slug, "recruiter@example.com", "Recruiter", "Let's talk.");

            Assert.Equal(RequestInterviewOutcome.EmailProviderPendingReview, result.Outcome);
            Assert.Equal(0, await db.InterviewRequests.CountAsync());
        }
        finally
        {
            await CleanupSeedDataAsync(db, slug);
        }
    }

    private static DevLogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(DataSource, npgsqlOptions => npgsqlOptions.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new DevLogDbContext(options);
    }

    private static void SeedPortfolio(DevLogDbContext db, string slug)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            UserId = $"user-{tenantId:N}",
            Slug = slug
        });
        db.Portfolios.Add(new Portfolio
        {
            TenantId = tenantId,
            Visibility = PortfolioVisibility.Public
        });
        db.SaveChanges();
    }

    private static NpgsqlDataSource BuildDataSource()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        dataSourceBuilder.UseVector();
        return dataSourceBuilder.Build();
    }

    private static async Task<bool> IsDatabaseAvailableAsync()
    {
        try
        {
            await using var connection = await DataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractToken(string emailBody)
    {
        var match = Regex.Match(emailBody, @"[?&]token=([A-Fa-f0-9]+)");
        Assert.True(match.Success);
        return match.Groups[1].Value;
    }

    private static async Task CleanupSeedDataAsync(DevLogDbContext db, string slug)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tenant is null)
        {
            return;
        }

        var requests = await db.InterviewRequests.Where(r => r.TenantId == tenant.Id).ToListAsync();
        var portfolios = await db.Portfolios.Where(p => p.TenantId == tenant.Id).ToListAsync();

        db.InterviewRequests.RemoveRange(requests);
        db.Portfolios.RemoveRange(portfolios);
        db.Tenants.Remove(tenant);

        await db.SaveChangesAsync();
    }

    private sealed class ThrowingEmailProvider : IEmailProvider
    {
        public Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("email send failed");
        }
    }

    private sealed class CapturingEmailProvider : IEmailProvider
    {
        public string? LastBody { get; private set; }

        public Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            LastBody = textBody;
            return Task.CompletedTask;
        }
    }

    private sealed class Throwing422EmailProvider : IEmailProvider
    {
        public Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("unprocessable", null, System.Net.HttpStatusCode.UnprocessableEntity);
        }
    }
}
