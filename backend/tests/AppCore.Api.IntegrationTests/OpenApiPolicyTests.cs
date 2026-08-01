using System.Net;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppCore.Api.IntegrationTests;

public sealed class OpenApiPolicyTests(
    WebApplicationFactory<Program> baseFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OpenApiIsDisabledByDefaultInProduction()
    {
        using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("AllowedHosts", "localhost");
                builder.UseSetting(
                    "Cors:AllowedOrigins:0",
                    "https://localhost:5173");
                builder.UseSetting(
                    "Database:ConnectionString",
                    BuildUnavailableDatabaseConnectionString());
                ConfigureProductionDataProtection(builder);
            });
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using HttpResponseMessage response =
            await client.GetAsync("/openapi/v1.json", timeout.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.example.com")]
    [InlineData("0.0.0.0")]
    [InlineData("[::]")]
    [InlineData("https://api.example.com")]
    [InlineData("api.example.com/path")]
    [InlineData("api.example.com:443")]
    public void ProductionRejectsUnsafeAllowedHostsAtStartup(
        string allowedHosts)
    {
        using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("AllowedHosts", allowedHosts);
                builder.UseSetting(
                    "Cors:AllowedOrigins:0",
                    "https://localhost:5173");
                builder.UseSetting(
                    "Database:ConnectionString",
                    BuildUnavailableDatabaseConnectionString());
                ConfigureProductionDataProtection(builder);
            });

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(factory.CreateClient);

        Assert.Contains(
            "AllowedHosts must contain explicit host names",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RecreatedHostOptionsRejectChangedUnsafeConfiguration()
    {
        using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("AllowedHosts", "localhost");
                builder.UseSetting(
                    "Cors:AllowedOrigins:0",
                    "https://localhost:5173");
                builder.UseSetting(
                    "Database:ConnectionString",
                    BuildUnavailableDatabaseConnectionString());
                ConfigureProductionDataProtection(builder);
            });
        _ = factory.CreateClient();

        IConfiguration configuration =
            factory.Services.GetRequiredService<IConfiguration>();
        configuration["AllowedHosts"] = "*";
        IOptionsFactory<HostFilteringOptions> optionsFactory =
            factory.Services
                .GetRequiredService<IOptionsFactory<HostFilteringOptions>>();

        Assert.Throws<OptionsValidationException>(
            () => optionsFactory.Create(Options.DefaultName));
    }

    [Fact]
    public async Task ProductionHostFailuresDoNotIncludeMiddlewareDetails()
    {
        using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("AllowedHosts", "localhost");
                builder.UseSetting(
                    "Cors:AllowedOrigins:0",
                    "https://localhost:5173");
                builder.UseSetting(
                    "Database:ConnectionString",
                    BuildUnavailableDatabaseConnectionString());
                ConfigureProductionDataProtection(builder);
            });
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });

        HostFilteringOptions options = factory.Services
            .GetRequiredService<IOptions<HostFilteringOptions>>()
            .Value;
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health/live");
        request.Headers.Host = "untrusted.example.test";
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.False(options.IncludeFailureMessage);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    private static string BuildUnavailableDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "unused",
            Username = "unused",
        }.ConnectionString;

    private static void ConfigureProductionDataProtection(
        IWebHostBuilder builder)
    {
        builder.UseSetting(
            "DataProtection:KeyStoragePath",
            Path.GetFullPath(Path.GetTempPath()));
        builder.UseSetting(
            "DataProtection:CertificateThumbprint",
            "0000000000000000000000000000000000000000");
        builder.UseSetting(
            "SecurityKeys:ChallengeHmacKeyBase64",
            Convert.ToBase64String(new byte[32]));
    }
}
