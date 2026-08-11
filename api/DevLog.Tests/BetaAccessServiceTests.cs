using System.Net;
using System.Security.Claims;
using System.Text;
using DevLog.Infrastructure;
using DevLog.Infrastructure.BetaAccess;
using DevLog.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace DevLog.Tests;

public class BetaAccessServiceTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=devlog;Username=devlog_app;Password=devlog_pw";
    private static readonly NpgsqlDataSource DataSource = BuildDataSource();

    [Fact]
    public async Task BetaDisabled_AllowsAccessWithoutEmailClaim()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        var db = CreateDbContext();
        var clerk = CreateClerkUserService("""{ "email_addresses": [] }""");
        var accessor = BuildHttpContextAccessor(new[]
        {
            new Claim("sub", "user_789")
        });
        var options = Options.Create(new PrivateBetaOptions
        {
            Enabled = false,
            RequireVerifiedEmail = true
        });

        var service = new BetaAccessService(db, clerk, accessor, options);
        var result = await service.EvaluateCurrentUserAsync();

        Assert.Equal(BetaAccessOutcome.BetaDisabled, result.Outcome);
        Assert.Null(result.ResolvedEmail);
    }

    [Fact]
    public async Task MissingEmailClaim_AdminFromClerk_IsAllowed()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        var db = CreateDbContext();
        var clerk = CreateClerkUserService("""
            {
              "primary_email_address_id": "idn_primary",
              "email_addresses": [
                {
                  "id": "idn_primary",
                  "email_address": "admin@example.com",
                  "verification": { "status": "verified" }
                }
              ]
            }
            """);
        var accessor = BuildHttpContextAccessor(new[]
        {
            new Claim("sub", "user_123")
        });
        var options = Options.Create(new PrivateBetaOptions
        {
            Enabled = true,
            RequireVerifiedEmail = true,
            AdminEmails = ["admin@example.com"]
        });

        var service = new BetaAccessService(db, clerk, accessor, options);
        var result = await service.EvaluateCurrentUserAsync();

        Assert.Equal(BetaAccessOutcome.Allowed, result.Outcome);
        Assert.Equal("admin@example.com", result.ResolvedEmail);

    [Fact]
    public async Task MissingVerifiedClaim_FallsBackToClerkVerification()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        var db = CreateDbContext();
        db.InviteAllowlistEntries.Add(new DevLog.Domain.Entities.InviteAllowlistEntry
        {
            Id = Guid.NewGuid(),
            Email = "candidate@example.com",
            NormalizedEmail = "candidate@example.com",
            Status = DevLog.Domain.Entities.InviteAllowlistStatus.Active
        });
        await db.SaveChangesAsync();

        var clerk = CreateClerkUserService("""
            {
              "primary_email_address_id": "idn_primary",
              "email_addresses": [
                {
                  "id": "idn_primary",
                  "email_address": "candidate@example.com",
                  "verification": { "status": "verified" }
                }
              ]
            }
            """);
        var accessor = BuildHttpContextAccessor(new[]
        {
            new Claim("sub", "user_456")
        });
        var options = Options.Create(new PrivateBetaOptions
        {
            Enabled = true,
            RequireVerifiedEmail = true
        });

        var service = new BetaAccessService(db, clerk, accessor, options);
        var result = await service.EvaluateCurrentUserAsync();

        Assert.Equal(BetaAccessOutcome.Allowed, result.Outcome);
        Assert.Equal("candidate@example.com", result.ResolvedEmail);
    }

    private static DevLogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(DataSource, npgsqlOptions => npgsqlOptions.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new DevLogDbContext(options);
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

    private static ClerkUserService CreateClerkUserService(string userJson)
    {
        var httpClient = new HttpClient(new StaticResponseHandler(userJson))
        {
            BaseAddress = new Uri("https://api.clerk.com/")
        };
        var options = Options.Create(new ClerkOptions
        {
            SecretKey = "sk_test_dummy"
        });
        return new ClerkUserService(httpClient, options, NullLogger<ClerkUserService>.Instance);
    }

    private static IHttpContextAccessor BuildHttpContextAccessor(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private sealed class StaticResponseHandler(string responseBody) : HttpMessageHandler
    {
        private readonly string _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
    }
}
