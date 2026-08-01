using System.Globalization;
using System.Security.Claims;
using AppCore.Api.Security;
using AppCore.Application.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppCore.Api.IntegrationTests;

public sealed class AuthenticatedSessionMiddlewareTests
{
    [Fact]
    public async Task DownstreamExceptionIsNotConvertedToSessionUnavailable()
    {
        Guid userId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        var expected = new InvalidOperationException("downstream failure");
        var middleware = new AuthenticatedSessionMiddleware(
            _ => throw expected,
            NullLogger<AuthenticatedSessionMiddleware>.Instance);
        DefaultHttpContext context = CreateAuthenticatedContext(
            userId,
            sessionId,
            authorizationVersion: 3);
        var validator = new StubSessionValidator(
            new ValidatedSession(
                sessionId,
                userId,
                3,
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow,
                null,
                "password"));

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => middleware.InvokeAsync(context, validator));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task PrincipalUserMustOwnValidatedSession()
    {
        Guid sessionId = Guid.NewGuid();
        var middleware = new AuthenticatedSessionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<AuthenticatedSessionMiddleware>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton<IProblemDetailsService, StubProblemDetailsService>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        DefaultHttpContext context = CreateAuthenticatedContext(
            Guid.NewGuid(),
            sessionId,
            authorizationVersion: 3);
        context.RequestServices = provider;
        context.Response.Body = new MemoryStream();
        var validator = new StubSessionValidator(
            new ValidatedSession(
                sessionId,
                Guid.NewGuid(),
                3,
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow,
                null,
                "password"));

        await middleware.InvokeAsync(context, validator);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task PublicEndpointDoesNotTouchSessionActivity()
    {
        Guid userId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        var validator = new StubSessionValidator(
            CreateValidatedSession(userId, sessionId));
        DefaultHttpContext context =
            CreateAuthenticatedContext(userId, sessionId, 3);
        context.Items[nameof(ValidatedSession)] =
            CreateValidatedSession(userId, sessionId);
        context.SetEndpoint(
            new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new AllowAnonymousAttribute()),
                "public"));
        var middleware = new SessionActivityMiddleware(
            _ => Task.CompletedTask,
            NullLogger<SessionActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context, validator);

        Assert.Equal(0, validator.TouchCount);
    }

    [Fact]
    public async Task AuthorizedProtectedEndpointTouchesSessionActivity()
    {
        Guid userId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        ValidatedSession session = CreateValidatedSession(userId, sessionId);
        var validator = new StubSessionValidator(session);
        DefaultHttpContext context =
            CreateAuthenticatedContext(userId, sessionId, 3);
        context.Items[nameof(ValidatedSession)] = session;
        context.SetEndpoint(
            new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizeAttribute()),
                "protected"));
        var middleware = new SessionActivityMiddleware(
            _ => Task.CompletedTask,
            NullLogger<SessionActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context, validator);

        Assert.Equal(1, validator.TouchCount);
    }

    [Fact]
    public async Task RevokedCookieDoesNotBlockAnonymousEndpoint()
    {
        bool nextCalled = false;
        DefaultHttpContext context = await CreateStaleCookieContextAsync(
            new EndpointMetadataCollection(new AllowAnonymousAttribute()));
        var middleware = new AuthenticatedSessionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<AuthenticatedSessionMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            new StubSessionValidator(session: null));

        Assert.True(nextCalled);
        Assert.False(context.User.Identity?.IsAuthenticated);
        Assert.Contains(
            AuthenticationSchemes.SessionCookieName,
            context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task RevokedCookieDoesNotBlockRecoveryEndpoint()
    {
        bool nextCalled = false;
        DefaultHttpContext context = await CreateStaleCookieContextAsync(
            new EndpointMetadataCollection(
                new AuthorizeAttribute
                {
                    AuthenticationSchemes = AuthenticationSchemes.Recovery,
                }));
        var middleware = new AuthenticatedSessionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<AuthenticatedSessionMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            new StubSessionValidator(session: null));

        Assert.True(nextCalled);
        Assert.False(context.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task NamedRecoveryPolicyBypassesNormalSessionValidation()
    {
        bool nextCalled = false;
        DefaultHttpContext context = await CreateStaleCookieContextAsync(
            new EndpointMetadataCollection(
                new AuthorizeAttribute("RecoveryOnly")));
        var middleware = new AuthenticatedSessionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<AuthenticatedSessionMiddleware>.Instance);
        var validator = new StubSessionValidator(session: null);

        await middleware.InvokeAsync(context, validator);

        Assert.True(nextCalled);
        Assert.Equal(0, validator.ValidateCount);
    }

    [Fact]
    public async Task RevokedCookieOnProtectedEndpointIsClearedAndRejected()
    {
        DefaultHttpContext context = await CreateStaleCookieContextAsync(
            new EndpointMetadataCollection(new AuthorizeAttribute()));
        var middleware = new AuthenticatedSessionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<AuthenticatedSessionMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            new StubSessionValidator(session: null));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(
            AuthenticationSchemes.SessionCookieName,
            context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task TouchStoreFailureReturnsServiceUnavailable()
    {
        Guid userId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        ValidatedSession session = CreateValidatedSession(userId, sessionId);
        DefaultHttpContext context = await CreateStaleCookieContextAsync(
            new EndpointMetadataCollection(new AuthorizeAttribute()));
        context.Items[nameof(ValidatedSession)] = session;
        var middleware = new SessionActivityMiddleware(
            _ => Task.CompletedTask,
            NullLogger<SessionActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context, new ThrowingTouchValidator(session));

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            context.Response.StatusCode);
    }

    private static async Task<DefaultHttpContext> CreateStaleCookieContextAsync(
        EndpointMetadataCollection metadata)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IProblemDetailsService, StubProblemDetailsService>();
        services
            .AddAuthentication()
            .AddCookie(AuthenticationSchemes.Session);
        ServiceProvider provider = services.BuildServiceProvider();
        DefaultHttpContext context = CreateAuthenticatedContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3);
        context.RequestServices = provider;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(
            new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        await Task.CompletedTask;
        return context;
    }

    private static ValidatedSession CreateValidatedSession(
        Guid userId,
        Guid sessionId) =>
        new(
            sessionId,
            userId,
            3,
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow,
            null,
            "password");

    private static DefaultHttpContext CreateAuthenticatedContext(
        Guid userId,
        Guid sessionId,
        long authorizationVersion)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(AuthenticationSchemes.SessionIdClaim, sessionId.ToString()),
                new Claim(
                    AuthenticationSchemes.AuthorizationVersionClaim,
                    authorizationVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            AuthenticationSchemes.Session);
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
    }

    private sealed class StubSessionValidator(ValidatedSession? session)
        : ISessionValidator
    {
        public int TouchCount { get; private set; }
        public int ValidateCount { get; private set; }

        public Task<ValidatedSession?> ValidateAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            return Task.FromResult<ValidatedSession?>(session);
        }

        public Task<bool> TouchAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default)
        {
            TouchCount++;
            return Task.FromResult(true);
        }

        public Task<bool> RecheckAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubProblemDetailsService : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(
            ProblemDetailsContext context) =>
            ValueTask.FromResult(true);

        public ValueTask WriteAsync(ProblemDetailsContext context) =>
            ValueTask.CompletedTask;
    }

    private sealed class ThrowingTouchValidator(ValidatedSession session)
        : ISessionValidator
    {
        public Task<ValidatedSession?> ValidateAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ValidatedSession?>(session);

        public Task<bool> TouchAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");

        public Task<bool> RecheckAsync(
            Guid sessionId,
            long expectedAuthorizationVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
