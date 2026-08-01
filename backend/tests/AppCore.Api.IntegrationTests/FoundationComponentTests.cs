using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AppCore.Api.Configuration;
using AppCore.Api.RateLimiting;
using AppCore.Api.Security;
using AppCore.Api.Validation;
using AppCore.Application.Security;
using AppCore.Contracts.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppCore.Api.IntegrationTests;

public sealed class FoundationComponentTests
{
    [Fact]
    public async Task PermissionPolicyProviderCreatesOnlyCatalogPolicies()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        AuthorizationPolicy? known = await provider.GetPolicyAsync(
            PermissionPolicies.For(SystemPermissions.UsersView));
        AuthorizationPolicy? unknown = await provider.GetPolicyAsync(
            PermissionPolicies.For("Unknown.Permission"));

        Assert.NotNull(known);
        Assert.Contains(
            known.Requirements,
            value => value is PermissionRequirement requirement
                && requirement.PermissionId == SystemPermissions.UsersView);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task ValidationFilterReturnsProblemDetailsForInvalidContract()
    {
        var httpContext = new DefaultHttpContext();
        var invocationContext = new TestInvocationContext(
            httpContext,
            [new ListQueryRequest { PageSize = 101 }]);
        var filter = new DataAnnotationsValidationFilter();

        object? result = await filter.InvokeAsync(
            invocationContext,
            _ => ValueTask.FromResult<object?>(Results.Ok()));
        IStatusCodeHttpResult validationResult =
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            validationResult.StatusCode);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problem = Assert.IsType<HttpValidationProblemDetails>(
            valueResult.Value);
        Assert.Equal(
            ["validation.range"],
            problem.Errors[nameof(ListQueryRequest.PageSize)]);
    }

    [Fact]
    public void RedactorMasksSensitiveStructuredValues()
    {
        var redactor = new SensitiveDataRedactor(
            Options.Create(new LoggingRedactionSettings()));

        Assert.Equal(
            SensitiveDataRedactor.RedactedValue,
            redactor.Redact("Authorization", "Bearer value"));
        Assert.Equal("safe", redactor.Redact("RecordType", "safe"));
    }

    [Fact]
    public void PagedResponseCalculatesTotalPages()
    {
        var response = new PagedResponse<int>(
            [1, 2],
            PageNumber: 2,
            PageSize: 20,
            TotalCount: 41);

        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public async Task SensitiveRateLimiterKeepsClientPartitionsIndependent()
    {
        var settings = new RateLimitingSettings
        {
            SensitivePermitLimit = 1,
            SensitiveWindowSeconds = 60,
        };
        using PartitionedRateLimiter<HttpContext> limiter =
            PartitionedRateLimiter.Create<HttpContext, string>(
                context => RateLimitingPolicies.CreateSensitivePartition(
                    context,
                    settings));
        var clientA = new DefaultHttpContext();
        clientA.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        var clientB = new DefaultHttpContext();
        clientB.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.11");

        using RateLimitLease firstA =
            await limiter.AcquireAsync(clientA, 1);
        using RateLimitLease secondA =
            await limiter.AcquireAsync(clientA, 1);
        using RateLimitLease firstB =
            await limiter.AcquireAsync(clientB, 1);

        Assert.True(firstA.IsAcquired);
        Assert.False(secondA.IsAcquired);
        Assert.True(firstB.IsAcquired);
    }

    [Fact]
    public void AuthenticatedActorTakesPrecedenceForRateLimitPartition()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "actor-42")],
                    "test")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        Assert.Equal(
            "actor:actor-42",
            RateLimitPartitionKeyResolver.Resolve(context));
    }

    private sealed class TestInvocationContext(
        HttpContext httpContext,
        IList<object?> arguments)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;

        public override IList<object?> Arguments { get; } = arguments;

        public override T GetArgument<T>(int index) =>
            (T)Arguments[index]!;
    }
}
