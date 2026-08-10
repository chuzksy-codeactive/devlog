using DevLog.Api.CostControl;
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
            }),
            new StubGlobalCapService(_ => true));

        var context = BuildContext("POST", "/api/onboarding/resume-import");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalCapReached_ReturnsTooManyRequests()
    {
        var nextCalled = 0;
        var acquireCalls = 0;
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
            }),
            new StubGlobalCapService(limit =>
            {
                Assert.Equal(1, limit);
                acquireCalls++;
                return acquireCalls == 1;
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
            new StaticOptionsMonitor(new AiCostControlOptions { Enabled = true }),
            new StubGlobalCapService(_ => true));

        var context = BuildContext("GET", "/api/portfolio/demo");
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task GlobalLimit_UsesLatestOptionsValuePerRequest()
    {
        var options = new MutableOptionsMonitor(new AiCostControlOptions
        {
            Enabled = true,
            GlobalExpensiveRequestsPerMinute = 5
        });

        var seenLimits = new List<int>();
        var middleware = new AiCostControlMiddleware(
            _ => Task.CompletedTask,
            options,
            new StubGlobalCapService(limit =>
            {
                seenLimits.Add(limit);
                return true;
            }));

        var first = BuildContext("POST", "/api/onboarding/resume-import");
        await middleware.InvokeAsync(first);

        options.Set(new AiCostControlOptions
        {
            Enabled = true,
            GlobalExpensiveRequestsPerMinute = 9
        });

        var second = BuildContext("POST", "/api/onboarding/resume-import");
        await middleware.InvokeAsync(second);

        Assert.Equal([5, 9], seenLimits);
    }

    [Theory]
    [InlineData("POST", "/api/portfolio/demo/ask", true)]
    [InlineData("POST", "/api/entries/00000000-0000-0000-0000-000000000001/sanitize", true)]
    [InlineData("GET", "/api/onboarding/resume-import", false)]
    [InlineData("POST", "/api/portfolio/demo", false)]
    public async Task ExpensiveEndpointMatcher_HonorsExpectedPaths(string method, string path, bool shouldHitGlobalCap)
    {
        var nextCalled = false;
        var acquireCalls = 0;
        var middleware = new AiCostControlMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new StaticOptionsMonitor(new AiCostControlOptions
            {
                Enabled = true,
                GlobalExpensiveRequestsPerMinute = 99
            }),
            new StubGlobalCapService(_ =>
            {
                acquireCalls++;
                return true;
            }));

        var context = BuildContext(method, path);
        await middleware.InvokeAsync(context);

        Assert.Equal(shouldHitGlobalCap ? 1 : 0, acquireCalls);
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

    private sealed class MutableOptionsMonitor : IOptionsMonitor<AiCostControlOptions>
    {
        private AiCostControlOptions _value;

        public MutableOptionsMonitor(AiCostControlOptions value)
        {
            _value = value;
        }

        public AiCostControlOptions CurrentValue => _value;
        public AiCostControlOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<AiCostControlOptions, string?> listener) => new NoOpDisposable();
        public void Set(AiCostControlOptions value) => _value = value;
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class StubGlobalCapService : IGlobalAiRequestCapService
    {
        private readonly Func<int, bool> _resultFactory;

        public StubGlobalCapService(Func<int, bool> resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public Task<bool> TryAcquireAsync(string scope, int maxPerMinute, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_resultFactory(maxPerMinute));
        }
    }
}
