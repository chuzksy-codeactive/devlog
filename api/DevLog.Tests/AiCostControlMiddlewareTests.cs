using DevLog.Api.Middleware;
using DevLog.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevLog.Tests;

public class AiCostControlMiddlewareTests
{
    [Fact]
    public async Task KillSwitchEnabled_OnExpensiveEndpoint_ReturnsServiceUnavailable()
    {
        var middleware = new AiCostControlMiddleware(
            _ => Task.CompletedTask,
            new StaticOptionsMonitor(new AiCostControlOptions
            {
                Enabled = true,
                KillSwitchEnabled = true
            }));

        var context = BuildContext("POST", "/api/onboarding/resume-import");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalCapReached_ReturnsTooManyRequests()
    {
        var nextCalled = 0;
        var middleware = new AiCostControlMiddleware(
            _ =>
            {
                nextCalled++;
                return Task.CompletedTask;
            },
            new StaticOptionsMonitor(new AiCostControlOptions
            {
                Enabled = true,
                GlobalExpensiveRequestsPerMinute = 1
            }));

        var first = BuildContext("POST", "/api/onboarding/resume-import");
        first.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(first);

        var second = BuildContext("POST", "/api/onboarding/resume-import");
        second.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(second);

        Assert.Equal(1, nextCalled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
    }

    [Fact]
    public async Task NonExpensiveEndpoint_BypassesMiddleware()
    {
        var nextCalled = false;
        var middleware = new AiCostControlMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new StaticOptionsMonitor(new AiCostControlOptions { Enabled = true }));

        var context = BuildContext("GET", "/api/portfolio/demo");
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext BuildContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<AiCostControlOptions>
    {
        private readonly AiCostControlOptions _value;

        public StaticOptionsMonitor(AiCostControlOptions value)
        {
            _value = value;
        }

        public AiCostControlOptions CurrentValue => _value;
        public AiCostControlOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<AiCostControlOptions, string?> listener) => new NoOpDisposable();
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
