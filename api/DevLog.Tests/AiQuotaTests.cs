using System.Net;
using DevLog.Api.CostControl;
using DevLog.Api.Middleware;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.Ai.CostControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevLog.Tests;

public class AiQuotaTests
{
    [Theory]
    [InlineData(AiQuotaDecision.MinuteLimit, "ai_provider_minute_allowance_reached")]
    [InlineData(AiQuotaDecision.DailyLimit, "ai_daily_allowance_reached")]
    [InlineData(AiQuotaDecision.MonthlyLimit, "ai_monthly_allowance_reached")]
    public async Task RejectedAllowance_DoesNotSendProviderRequest(AiQuotaDecision decision, string reason)
    {
        var store = new StubQuotaStore { Decision = decision };
        var terminal = new CountingHandler();
        using var client = CreateClient(store, terminal);

        var error = await Assert.ThrowsAsync<AiQuotaRejectedException>(
            () => client.PostAsync("https://example.invalid", new StringContent("test")));

        Assert.Equal(reason, error.Reason);
        Assert.Equal(0, terminal.Calls);
        Assert.Equal(("provider_calls", 20, (int?)100, (int?)1000), Assert.Single(store.Calls));
    }

    [Fact]
    public async Task AllowedRequest_IsReservedBeforeSending_AndFailuresAreNotRefunded()
    {
        var store = new StubQuotaStore();
        var terminal = new CountingHandler
        {
            OnSend = () =>
            {
                Assert.Single(store.Calls);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
        };
        using var client = CreateClient(store, terminal);

        using var response = await client.PostAsync("https://example.invalid", new StringContent("test"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(store.Calls);
        Assert.Equal(1, terminal.Calls);
    }

    [Fact]
    public async Task DisabledByDefault_PreservesProviderBehavior()
    {
        var store = new StubQuotaStore { Error = new AiQuotaUnavailableException("Must not be consulted.") };
        var terminal = new CountingHandler();
        using var client = CreateClient(store, terminal, new AiQuotaOptions());

        using var response = await client.GetAsync("https://example.invalid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public async Task UnavailableStore_FailsClosed()
    {
        var store = new StubQuotaStore { Error = new AiQuotaUnavailableException("Database unavailable.") };
        var terminal = new CountingHandler();
        using var client = CreateClient(store, terminal);

        await Assert.ThrowsAsync<AiQuotaUnavailableException>(() => client.GetAsync("https://example.invalid"));

        Assert.Equal(0, terminal.Calls);
    }

    [Fact]
    public async Task KillSwitch_BlocksAllProviderCallsBeforeReservation()
    {
        var store = new StubQuotaStore();
        var terminal = new CountingHandler();
        using var client = CreateClient(store, terminal, killSwitch: true);

        await Assert.ThrowsAsync<AiQuotaPausedException>(() => client.GetAsync("https://example.invalid"));

        Assert.Empty(store.Calls);
        Assert.Equal(0, terminal.Calls);
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Anthropic")]
    public async Task RegisteredProviders_CoverGenerationImportsAndEmbeddings(string provider)
    {
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AiQuotas:Enabled"] = "true",
            ["Ai:GenerationProvider"] = provider
        });
        var store = new StubQuotaStore { Decision = AiQuotaDecision.MonthlyLimit };
        var terminal = new CountingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddDevLogInfrastructure(config, isDevelopment: true);
        services.AddSingleton<IAiQuotaStore>(store);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => terminal));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var llm = scope.ServiceProvider.GetRequiredService<ILlmProvider>();
        var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();

        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => llm.SanitizeAsync("entry", []));
        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => llm.AnswerQuestionAsync("question", []));
        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => llm.SuggestOnboardingContentAsync("github"));
        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => llm.SuggestOnboardingContentFromResumeAsync("resume"));
        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => embedding.EmbedAsync("entry"));
        await Assert.ThrowsAsync<AiQuotaRejectedException>(() => embedding.EmbedBatchAsync(["first", "second"]));
        Assert.Empty(await embedding.EmbedBatchAsync([]));

        Assert.Equal(6, store.Calls.Count);
        Assert.Equal(0, terminal.Calls);
    }

    [Fact]
    public void EnabledQuotas_RejectInvalidConfiguration()
    {
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AiQuotas:Enabled"] = "true",
            ["AiQuotas:MaxProviderCallsPerDay"] = "0"
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevLogInfrastructure(config, isDevelopment: true);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AiQuotaOptions>>().Value);
    }

    [Theory]
    [InlineData(AiQuotaDecision.Allowed, true)]
    [InlineData(AiQuotaDecision.MinuteLimit, false)]
    public async Task RequestCounter_UsesSeparateScope(AiQuotaDecision decision, bool expected)
    {
        var store = new StubQuotaStore { Decision = decision };
        var service = new PostgresGlobalAiRequestCapService(store);

        Assert.Equal(expected, await service.TryAcquireAsync("expensive", 7));
        Assert.Equal(("requests", 7, (int?)null, (int?)null), Assert.Single(store.Calls));
    }

    [Theory]
    [InlineData("denied", 429, "ai_daily_allowance_reached")]
    [InlineData("unavailable", 503, "quota_store_unavailable")]
    [InlineData("paused", 503, "kill_switch_enabled")]
    public async Task QuotaErrors_ReturnExplicitSafeResponses(string kind, int expectedStatus, string reason)
    {
        Exception exception = kind switch
        {
            "denied" => new AiQuotaRejectedException("ai_daily_allowance_reached"),
            "paused" => new AiQuotaPausedException(),
            _ => new AiQuotaUnavailableException("Sensitive internal details")
        };
        var middleware = new AiQuotaExceptionMiddleware(
            _ => Task.FromException(exception), NullLogger<AiQuotaExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Access-Control-Allow-Origin"] = "https://app.example";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        context.Response.ContentLength = 500;

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("https://app.example", context.Response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("max-age=31536000", context.Response.Headers["Strict-Transport-Security"]);
        Assert.Null(context.Response.ContentLength);
        Assert.Contains(reason, body);
        Assert.DoesNotContain("Sensitive internal details", body);
    }

    internal static IConfiguration CreateConfiguration(Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DevLogDbRuntime"] = "Host=localhost;Database=unused;Username=unused",
            ["IpHashing:Secret"] = "quota-tests-only-not-a-production-secret-value"
        };
        foreach (var item in extra ?? [])
        {
            values[item.Key] = item.Value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static HttpClient CreateClient(
        StubQuotaStore store, CountingHandler terminal, AiQuotaOptions? options = null, bool killSwitch = false)
    {
        var handler = new AiQuotaHttpHandler(
            store,
            new ConstantOptions<AiQuotaOptions>(options ?? new AiQuotaOptions { Enabled = true }),
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["AiCostControl:KillSwitchEnabled"] = killSwitch.ToString()
            }))
        {
            InnerHandler = terminal
        };
        return new HttpClient(handler);
    }

    private sealed class StubQuotaStore : IAiQuotaStore
    {
        public AiQuotaDecision Decision { get; init; } = AiQuotaDecision.Allowed;
        public Exception? Error { get; init; }
        public List<(string, int, int?, int?)> Calls { get; } = [];

        public Task<AiQuotaDecision> TryAcquireAsync(
            string scope, int minuteLimit, int? dailyLimit = null, int? monthlyLimit = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((scope, minuteLimit, dailyLimit, monthlyLimit));
            return Error is null ? Task.FromResult(Decision) : Task.FromException<AiQuotaDecision>(Error);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Func<HttpResponseMessage>? OnSend { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(OnSend?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class ConstantOptions<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
