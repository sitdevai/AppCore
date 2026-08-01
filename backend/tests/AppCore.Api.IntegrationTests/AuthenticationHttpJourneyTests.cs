using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AppCore.Api;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AppCore.Api.IntegrationTests;

public sealed class AuthenticationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string ConnectionString => database.GetConnectionString();
    public string SecurityKey { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        SecurityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Database:ConnectionString"] = ConnectionString,
                        ["SecurityKeys:CurrentVersion"] = "1",
                        ["SecurityKeys:Keys:1"] = SecurityKey,
                        ["RateLimiting:SensitivePermitLimit"] = "100",
                    }));
        });
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await database.DisposeAsync();
    }
}

public sealed class AuthenticationHttpJourneyTests(
    AuthenticationHttpFixture fixture)
    : IClassFixture<AuthenticationHttpFixture>
{
    private const string Password = "Correct Horse Battery Staple 2026!";

    [Fact]
    public async Task ActivationLoginMfaAndRecoveryRespectCookieAndCsrfBoundaries()
    {
        OneTimeChallengeResult activation;
        Guid ownerId;
        HostApplicationBuilder setupBuilder = Host.CreateApplicationBuilder();
        setupBuilder.Environment.EnvironmentName = Environments.Production;
        setupBuilder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = fixture.ConnectionString,
                ["SecurityKeys:CurrentVersion"] = "1",
                ["SecurityKeys:Keys:1"] = fixture.SecurityKey,
            });
        setupBuilder.Services.AddSetupServices(setupBuilder.Configuration);
        using IHost setupHost = setupBuilder.Build();
        await setupHost.StartAsync();
        await using (AsyncServiceScope scope =
            setupHost.Services.CreateAsyncScope())
        {
            BootstrapIdentityPreparationService bootstrap =
                scope.ServiceProvider.GetRequiredService<
                    BootstrapIdentityPreparationService>();
            activation = await bootstrap.CreateOwnerAsync("http-owner", null);
            ownerId = activation.UserId;
        }

        using HttpClient browser = CreateBrowser();
        (string anonymousToken, Guid preSessionId) =
            await StartAnonymousFlowAsync(browser);
        using HttpResponseMessage activationResponse = await PostAsync(
            browser,
            "/api/v1/auth/activation/complete",
            new
            {
                username = "http-owner",
                code = activation.Code,
                newPassword = Password,
                preSessionId,
            },
            anonymousToken);
        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);
        Assert.DoesNotContain(
            "AppCore.Session",
            activationResponse.Headers.ToString(),
            StringComparison.Ordinal);

        await using (AsyncServiceScope scope =
            fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider
                .GetRequiredService<BootstrapIdentityPreparationService>()
                .EnablePreparedOwnerAsync(ownerId));
        }

        (anonymousToken, preSessionId) = await StartAnonymousFlowAsync(browser);
        using HttpResponseMessage loginResponse = await PostAsync(
            browser,
            "/api/v1/auth/login",
            new { username = "http-owner", password = Password, preSessionId },
            anonymousToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            "AppCore.Session",
            loginResponse.Headers.ToString(),
            StringComparison.Ordinal);
        Assert.Equal(
            HttpStatusCode.OK,
            (await browser.GetAsync("/api/v1/auth/me")).StatusCode);

        string sessionCsrf = await GetCsrfAsync(browser, "/api/v1/auth/csrf");
        using HttpResponseMessage enrollmentResponse = await PostAsync(
            browser,
            "/api/v1/auth/mfa/enrollment",
            new { currentPassword = Password },
            sessionCsrf);
        JsonElement enrollment = await ReadJsonAsync(enrollmentResponse);
        Guid authenticatorId = enrollment.GetProperty("authenticatorId").GetGuid();
        string secret = enrollment.GetProperty("manualEntryKey").GetString()!;
        sessionCsrf = await GetCsrfAsync(browser, "/api/v1/auth/csrf");
        using HttpResponseMessage verificationResponse = await PostAsync(
            browser,
            "/api/v1/auth/mfa/enrollment/verify",
            new { authenticatorId, code = TotpCode(secret) },
            sessionCsrf);
        JsonElement verification = await ReadJsonAsync(verificationResponse);
        string recoveryCode =
            verification.GetProperty("recoveryCodes")[0].GetString()!;
        Assert.Equal(HttpStatusCode.OK, verificationResponse.StatusCode);

        using HttpClient recoveryBrowser = CreateBrowser();
        (anonymousToken, preSessionId) =
            await StartAnonymousFlowAsync(recoveryBrowser);
        using HttpResponseMessage recoveryResponse = await PostAsync(
            recoveryBrowser,
            "/api/v1/auth/recovery",
            new
            {
                username = "http-owner",
                password = Password,
                recoveryCode,
                preSessionId,
            },
            anonymousToken);
        Assert.Equal(HttpStatusCode.OK, recoveryResponse.StatusCode);
        Assert.Contains(
            "AppCore.Recovery",
            recoveryResponse.Headers.ToString(),
            StringComparison.Ordinal);

        string recoveryCsrf = await GetCsrfAsync(
            recoveryBrowser,
            "/api/v1/auth/recovery/csrf");
        using HttpResponseMessage recoveryEnrollment = await PostAsync(
            recoveryBrowser,
            "/api/v1/auth/recovery/mfa/enrollment",
            new { },
            recoveryCsrf);
        Assert.Equal(HttpStatusCode.OK, recoveryEnrollment.StatusCode);

        using HttpResponseMessage invalidCsrf = await PostAsync(
            recoveryBrowser,
            "/api/v1/auth/recovery/logout",
            new { },
            "invalid");
        Assert.Equal(HttpStatusCode.Forbidden, invalidCsrf.StatusCode);
        Assert.Equal(
            "application/problem+json",
            invalidCsrf.Content.Headers.ContentType?.MediaType);
    }

    private HttpClient CreateBrowser() =>
        fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
            });

    private static async Task<(string Token, Guid PreSessionId)>
        StartAnonymousFlowAsync(HttpClient client)
    {
        string token = await GetCsrfAsync(client, "/api/v1/auth/csrf");
        using HttpResponseMessage response = await PostAsync(
            client,
            "/api/v1/auth/pre-session",
            new { },
            token);
        JsonElement payload = await ReadJsonAsync(response);
        return (token, payload.GetProperty("preSessionId").GetGuid());
    }

    private static async Task<string> GetCsrfAsync(
        HttpClient client,
        string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        JsonElement payload = await ReadJsonAsync(response);
        return payload.GetProperty("requestToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object body,
        string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static string TotpCode(string encodedSecret)
    {
        byte[] secret = DecodeBase32(encodedSecret);
        long step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        byte[] counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

#pragma warning disable CA5350 // RFC 6238 interoperability requires HMAC-SHA1.
        byte[] hash = HMACSHA1.HashData(secret, counter);
#pragma warning restore CA5350
        int offset = hash[^1] & 0x0f;
        int binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString(
            "D6",
            CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        int buffer = 0;
        int bits = 0;
        foreach (char character in value)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. output];
    }
}
