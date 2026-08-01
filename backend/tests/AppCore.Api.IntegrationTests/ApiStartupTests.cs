using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppCore.Api.Middleware;
using AppCore.Api.Security;
using AppCore.Application.Security;
using AppCore.Contracts.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppCore.Api.IntegrationTests;

public sealed class ApiStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ApiStartupTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Database:ConnectionString"] =
                                BuildUnavailableDatabaseConnectionString(),
                        }));
        });
    }

    [Fact]
    public async Task DevelopmentHostStartsAndServesOpenApi()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IHostEnvironment environment =
            scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        Assert.True(environment.IsDevelopment());

        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using HttpResponseMessage response =
            await client.GetAsync("/openapi/v1.json", timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LiveHealthAndVersionedSystemEndpointAreAvailable()
    {
        using HttpClient client = CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using HttpResponseMessage healthResponse =
            await client.GetAsync("/health/live", timeout.Token);
        using HttpResponseMessage readinessResponse =
            await client.GetAsync("/health/ready", timeout.Token);
        using HttpResponseMessage systemResponse =
            await client.GetAsync("/api/v1/system/info", timeout.Token);
        SystemInfoResponse? systemInfo =
            await systemResponse.Content.ReadFromJsonAsync<SystemInfoResponse>(
                timeout.Token);

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            readinessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, systemResponse.StatusCode);
        Assert.NotNull(systemInfo);
        Assert.Equal("1.0", systemInfo.ApiVersion);
    }

    [Fact]
    public async Task CsrfBootstrapIsSafeAndDoesNotCreatePreSession()
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response =
            await client.GetAsync("/api/v1/auth/csrf");
        using JsonDocument payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.TryGetProperty(
            "requestToken",
            out _));
        Assert.False(payload.RootElement.TryGetProperty(
            "preSessionId",
            out _));
        Assert.Contains(
            "no-store",
            response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task MissingEndpointReturnsProblemDetailsWithCorrelationId()
    {
        using HttpClient client = CreateClient();
        const string correlationId = "integration-test-correlation";
        client.DefaultRequestHeaders.Add(
            CorrelationIdMiddleware.HeaderName,
            correlationId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using HttpResponseMessage response =
            await client.GetAsync("/missing", timeout.Token);
        string body = await response.Content.ReadAsStringAsync(timeout.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            correlationId,
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        Assert.Contains("\"status\":404", body, StringComparison.Ordinal);
        Assert.Contains(correlationId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorsExposesCorrelationHeaderToConfiguredWebClient()
    {
        using HttpClient client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/system/info");
        request.Headers.Add("Origin", "http://localhost:5173");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            CorrelationIdMiddleware.HeaderName,
            response.Headers.GetValues("Access-Control-Expose-Headers"));
    }

    [Fact]
    public async Task HostFilteringRejectsUnconfiguredHost()
    {
        using HttpClient client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health/live");
        request.Headers.Host = "untrusted.example.test";

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdministrationEndpointsRejectAnonymousBypass()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage users = await client.GetAsync(
            "/api/v1/administration/users");
        using HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/v1/administration/users",
            new { username = "bypass", confirmed = true });

        Assert.Equal(HttpStatusCode.Unauthorized, users.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/sessions/me")]
    [InlineData("/api/v1/security-audit")]
    public async Task SecurityAdministrationEndpointsRejectAnonymousBypass(string path)
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void AdministrationEndpointsCarryAtomicPermissionPolicies()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint users = source.Endpoints.OfType<RouteEndpoint>().Single(value =>
            value.RoutePattern.RawText == "/api/v{version:apiVersion}/administration/users"
            && value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET"));
        string[] policies = users.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(value => value.Policy)
            .OfType<string>()
            .ToArray();

        Assert.Contains(PermissionPolicies.For(SystemPermissions.UsersView), policies);
    }

    [Fact]
    public void GlobalSessionRevocationCarriesEmergencyPermissionPolicy()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>().Single(value =>
            value.RoutePattern.RawText == "/api/v{version:apiVersion}/sessions/revoke-global");
        string[] policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(value => value.Policy).OfType<string>().ToArray();
        Assert.Contains(
            PermissionPolicies.For(SystemPermissions.SessionsRevokeGlobal),
            policies);
    }

    [Fact]
    public void VisualIdentityMutationCarriesItsDedicatedHighRiskPolicy()
    {
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>().Single(value =>
            value.RoutePattern.RawText ==
                "/api/v{version:apiVersion}/settings/visual-identity/"
            && value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
        string[] policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(value => value.Policy).OfType<string>().ToArray();

        Assert.Contains(
            PermissionPolicies.For(SystemPermissions.SettingsVisualIdentityUpdate),
            policies);
        Assert.Equal(
            PermissionAssurance.HighRisk,
            SystemPermissions.Find(SystemPermissions.SettingsVisualIdentityUpdate)?.Assurance);
    }

    [Fact]
    public async Task VisualIdentityMutationRejectsAnonymousUsers()
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/settings/visual-identity/",
            new
            {
                organizationName = "Name",
                shortOrganizationName = "Short",
                primaryColor = "#112233",
                secondaryColor = "#445566",
                expectedVersion = 1,
                confirmed = true,
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void HostFilteringRejectsEmptyHostHeaders()
    {
        HostFilteringOptions options = factory.Services
            .GetRequiredService<IOptions<HostFilteringOptions>>()
            .Value;

        Assert.False(options.AllowEmptyHosts);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });

    private static string BuildUnavailableDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "unused",
            Username = "unused",
        }.ConnectionString;
}
