using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AppCore.Api.Security;
using AppCore.Application.Security;
using AppCore.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppCore.Api.IntegrationTests;

public sealed class Phase04ASecurityConfigurationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public Phase04ASecurityConfigurationTests(
        WebApplicationFactory<Program> factory)
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
                                new NpgsqlConnectionStringBuilder
                                {
                                    Host = "127.0.0.1",
                                    Port = 1,
                                    Database = "unused",
                                    Username = "unused",
                                }.ConnectionString,
                        }));
        });
    }

    [Fact]
    public void SessionAndRecoveryCookiesAreSecureAndIsolated()
    {
        IOptionsMonitor<CookieAuthenticationOptions> cookies =
            factory.Services.GetRequiredService<
                IOptionsMonitor<CookieAuthenticationOptions>>();

        CookieAuthenticationOptions session =
            cookies.Get(AuthenticationSchemes.Session);
        CookieAuthenticationOptions recovery =
            cookies.Get(AuthenticationSchemes.Recovery);

        Assert.Equal(AuthenticationSchemes.SessionCookieName, session.Cookie.Name);
        Assert.Equal(AuthenticationSchemes.RecoveryCookieName, recovery.Cookie.Name);
        Assert.True(session.Cookie.HttpOnly);
        Assert.True(recovery.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, session.Cookie.SecurePolicy);
        Assert.Equal(CookieSecurePolicy.Always, recovery.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, session.Cookie.SameSite);
        Assert.Equal("/", session.Cookie.Path);
        Assert.Null(session.Cookie.Domain);
        Assert.Equal(TimeSpan.FromMinutes(30), session.ExpireTimeSpan);
        Assert.Equal(TimeSpan.FromMinutes(15), recovery.ExpireTimeSpan);
        Assert.True(session.SlidingExpiration);
        Assert.False(recovery.SlidingExpiration);

        using IServiceScope scope = factory.Services.CreateScope();
        IdentityOptions identity =
            scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.False(identity.User.RequireUniqueEmail);
        Assert.Contains(
            scope.ServiceProvider.GetServices<IUserValidator<ApplicationUser>>(),
            validator => validator is OptionalUniqueEmailUserValidator);
    }

    [Fact]
    public void AntiforgeryUsesApprovedHeaderAndSecureHostCookie()
    {
        AntiforgeryOptions options =
            factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal("X-CSRF-TOKEN", options.HeaderName);
        Assert.Equal("__Host-AppCore.Antiforgery", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
    }

    [Fact]
    public async Task AntiforgeryInfrastructureIssuesAndValidatesHeaderToken()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IAntiforgery antiforgery =
            scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        var issuanceContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        issuanceContext.Request.Scheme = "https";

        AntiforgeryTokenSet tokens =
            antiforgery.GetAndStoreTokens(issuanceContext);
        Assert.NotNull(tokens.CookieToken);
        Assert.NotNull(tokens.RequestToken);

        var validationContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        validationContext.Request.Scheme = "https";
        validationContext.Request.Method = HttpMethods.Post;
        validationContext.Request.Headers.Cookie =
            $"__Host-AppCore.Antiforgery={tokens.CookieToken}";
        validationContext.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken;

        await antiforgery.ValidateRequestAsync(validationContext);
    }

    [Fact]
    public void MfaSecretsSurviveServiceProviderRestartWithSharedProtectedKeyRing()
    {
        string keyPath = Path.Combine(
            Path.GetTempPath(),
            $"app-core-keyring-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyPath);
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=AppCoreTests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        byte[] protectedSecret;
        using (ServiceProvider first = CreateProtectionProvider(
            keyPath,
            certificate))
        {
            protectedSecret = first
                .GetRequiredService<IMfaSecretProtector>()
                .Protect([1, 2, 3, 4]);
        }

        using (ServiceProvider second = CreateProtectionProvider(
            keyPath,
            certificate))
        {
            byte[] secret = second
                .GetRequiredService<IMfaSecretProtector>()
                .Unprotect(protectedSecret);
            Assert.Equal([1, 2, 3, 4], secret);
        }

        Directory.Delete(keyPath, recursive: true);
    }

    private static ServiceProvider CreateProtectionProvider(
        string keyPath,
        X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName("AppCore.Tests")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .ProtectKeysWithCertificate(certificate);
        services.AddSingleton<IMfaSecretProtector, MfaSecretProtector>();
        return services.BuildServiceProvider();
    }
}
