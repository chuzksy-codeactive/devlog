using System.Net;
using DevLog.Api.CostControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Xunit;

namespace DevLog.Tests;

public class AiQuotaStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_SelectsExpectedCounterWithoutConnectingToRedisWhenQuotasEnabled(bool enabled)
    {
        await using var factory = new QuotaApiFactory(enabled);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = factory.Services.GetRequiredService<IGlobalAiRequestCapService>();
        if (enabled)
        {
            Assert.IsType<PostgresGlobalAiRequestCapService>(service);
        }
        else
        {
            Assert.IsType<InMemoryGlobalAiRequestCapService>(service);
        }
        Assert.Null(factory.Services.GetService<IConnectionMultiplexer>());
    }

    private sealed class QuotaApiFactory(bool enabled) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiQuotas:Enabled"] = enabled.ToString(),
                    ["Redis:ConnectionString"] = enabled ? "this-is-not-a-valid-redis-configuration" : "",
                    ["ConnectionStrings:DevLogDbRuntime"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused",
                    ["IpHashing:Secret"] = "startup-tests-only-not-a-production-secret-value",
                    ["KeyVault:Enabled"] = "false",
                    ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "",
                    ["ApplicationInsights:ConnectionString"] = "",
                    ["Storage:Provider"] = "AzureBlob",
                    ["MalwareScanning:Provider"] = "AzureDefenderStorage"
                }));
            return base.CreateHost(builder);
        }
    }
}
