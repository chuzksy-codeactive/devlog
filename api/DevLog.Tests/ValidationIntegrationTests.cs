using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevLog.Tests;

public class ValidationIntegrationTests : IClassFixture<ValidationTestApiFactory>
{
    private readonly ValidationTestApiFactory _factory;

    public ValidationIntegrationTests(ValidationTestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateInterviewRequest_InvalidEmail_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/portfolio/test/interview-requests", new
        {
            recruiterEmail = "not-an-email",
            recruiterName = "Recruiter",
            note = "Interested in speaking with you"
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("RecruiterEmail", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateInterviewRequest_EmptyNote_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/portfolio/test/interview-requests", new
        {
            recruiterEmail = "recruiter@example.com",
            recruiterName = "Recruiter",
            note = ""
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Note", body, StringComparison.OrdinalIgnoreCase);
    }
}

public class ValidationTestApiFactory : WebApplicationFactory<Program>
{
    public ValidationTestApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DevLogDbRuntime",
            "Host=localhost;Port=5432;Database=devlog;Username=devlog_app;Password=devlog_app");
        Environment.SetEnvironmentVariable(
            "IpHashing__Secret",
            "D5F78A4C21E390B64FA2D8715C39BE04216A5FD3B2C91E87");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DevLogDbRuntime"] = "Host=localhost;Port=5432;Database=devlog;Username=devlog_app;Password=devlog_app_local",
                ["Clerk:Authority"] = "https://example.clerk.accounts.dev",
                ["Clerk:AuthorizedParty"] = "http://localhost:3000",
                ["Clerk:SecretKey"] = "sk_test_dummy",
                ["Storage:LocalDiskPath"] = Path.Combine(Path.GetTempPath(), "devlog-test-uploads"),
                ["Storage:PublicBaseUrl"] = "http://localhost:5000/uploads",
                ["IpHashing:Secret"] = "D5F78A4C21E390B64FA2D8715C39BE04216A5FD3B2C91E87"
            };

            configBuilder.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });
        });
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", "test-user"),
            new Claim(ClaimTypes.Name, "test-user")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
