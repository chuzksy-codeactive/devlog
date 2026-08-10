using DevLog.Api.Middleware;
using DevLog.Infrastructure.BetaAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace DevLog.Tests;

public class PrivateBetaAccessMiddlewareTests
{
    [Fact]
    public async Task ProtectedEndpoint_NotInvited_ReturnsForbiddenWithReason()
    {
        var middleware = new PrivateBetaAccessMiddleware(_ => Task.CompletedTask);
        var context = BuildContext(
            "/api/entries",
            new EndpointMetadataCollection(new AuthorizeAttribute()));

        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(
            context,
            new StubBetaAccessService(new BetaAccessResult(BetaAccessOutcome.NotInvited, "user@example.com")));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("private_beta_access_denied", payload, StringComparison.Ordinal);
        Assert.Contains("not_invited", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedEndpoint_Allowed_CallsNext()
    {
        var nextCalled = false;
        var middleware = new PrivateBetaAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = BuildContext(
            "/api/entries",
            new EndpointMetadataCollection(new AuthorizeAttribute()));

        await middleware.InvokeAsync(
            context,
            new StubBetaAccessService(new BetaAccessResult(BetaAccessOutcome.Allowed, "user@example.com")));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task AllowAnonymousEndpoint_BypassesBetaGate()
    {
        var nextCalled = false;
        var middleware = new PrivateBetaAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = BuildContext(
            "/api/portfolio/demo",
            new EndpointMetadataCollection(new AuthorizeAttribute(), new AllowAnonymousAttribute()));

        await middleware.InvokeAsync(
            context,
            new StubBetaAccessService(new BetaAccessResult(BetaAccessOutcome.NotInvited, "user@example.com")));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task AdminEndpoint_BypassesBetaGate()
    {
        var nextCalled = false;
        var middleware = new PrivateBetaAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = BuildContext(
            "/api/beta-access/admin/waitlist",
            new EndpointMetadataCollection(new AuthorizeAttribute()));

        await middleware.InvokeAsync(
            context,
            new StubBetaAccessService(new BetaAccessResult(BetaAccessOutcome.NotInvited, "user@example.com")));

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext BuildContext(string path, EndpointMetadataCollection metadata)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test-endpoint"));
        return context;
    }

    private sealed class StubBetaAccessService : IBetaAccessService
    {
        private readonly BetaAccessResult _result;

        public StubBetaAccessService(BetaAccessResult result)
        {
            _result = result;
        }

        public Task<BetaAccessResult> EvaluateCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }
}
