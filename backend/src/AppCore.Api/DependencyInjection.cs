using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Asp.Versioning;
using AppCore.Api.Configuration;
using AppCore.Api.ErrorHandling;
using AppCore.Api.Middleware;
using AppCore.Api.RateLimiting;
using AppCore.Api.Security;
using AppCore.Application.Common.Abstractions;
using AppCore.Application.Security;
using AppCore.Infrastructure;
using AppCore.Infrastructure.Branding;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ApiAuthenticationSchemes = AppCore.Api.Security.AuthenticationSchemes;

namespace AppCore.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<HostFilteringOptions>()
            .Configure(options =>
            {
                options.AllowEmptyHosts = false;
                options.IncludeFailureMessage =
                    !environment.IsProduction();
            })
            .Validate(
                options =>
                    options.AllowedHosts.Count > 0
                    && options.AllowedHosts.All(IsExplicitHostName),
                "AllowedHosts must contain explicit host names without wildcards, schemes, paths, or ports.")
            .ValidateOnStart();

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                DatabaseOptions.HasValidConnectionString,
                "Database:ConnectionString must be a valid PostgreSQL connection string with host, database, and username.")
            .ValidateOnStart();

        CorsSettings cors = configuration
            .GetRequiredSection(CorsSettings.SectionName)
            .Get<CorsSettings>()
            ?? throw new InvalidOperationException("CORS configuration is missing.");
        ValidateAllowedOrigins(cors.AllowedOrigins);
        services
            .AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .Validate(
                options => options.AllowedOrigins.Length > 0,
                "At least one explicit CORS origin is required.")
            .ValidateOnStart();

        services
            .AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        RateLimitingSettings rateLimiting = configuration
            .GetRequiredSection(RateLimitingSettings.SectionName)
            .Get<RateLimitingSettings>()
            ?? throw new InvalidOperationException(
                "Rate-limiting configuration is missing.");

        ForwardedHeadersSettings forwardedHeaders = configuration
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();
        IPAddress[] knownProxies = ParseKnownProxies(
            forwardedHeaders.KnownProxies);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto;
            foreach (IPAddress knownProxy in knownProxies)
            {
                options.KnownProxies.Add(knownProxy);
            }
        });

        services
            .AddOptions<OpenApiSettings>()
            .Bind(configuration.GetSection(OpenApiSettings.SectionName))
            .ValidateOnStart();
        services
            .AddOptions<LoggingRedactionSettings>()
            .Bind(configuration.GetSection(LoggingRedactionSettings.SectionName))
            .Validate(
                options => options.SensitiveKeys.Length > 0,
                "At least one sensitive logging key is required.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        DataProtectionSettings dataProtection = configuration
            .GetRequiredSection(DataProtectionSettings.SectionName)
            .Get<DataProtectionSettings>()
            ?? throw new InvalidOperationException(
                "Data Protection configuration is missing.");
        OptionsBuilder<DataProtectionSettings> dataProtectionOptions = services
            .AddOptions<DataProtectionSettings>()
            .Bind(configuration.GetSection(DataProtectionSettings.SectionName))
            .ValidateDataAnnotations();
        if (environment.IsProduction())
        {
            dataProtectionOptions.Validate(
                DataProtectionSettings.HasProductionProtection,
                "Production Data Protection requires an application name, an absolute shared key-storage path, and an existing password-protected key-encryption certificate.");
        }

        dataProtectionOptions.ValidateOnStart();
        IDataProtectionBuilder dataProtectionBuilder = services
            .AddDataProtection()
            .SetApplicationName(dataProtection.ApplicationName);
        if (!string.IsNullOrWhiteSpace(dataProtection.KeyStoragePath))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(
                new DirectoryInfo(dataProtection.KeyStoragePath));
        }

        if (!string.IsNullOrWhiteSpace(dataProtection.CertificateThumbprint))
        {
            services
                .AddOptions<KeyManagementOptions>()
                .Configure<ILoggerFactory>(
                    (options, loggerFactory) =>
                        options.XmlEncryptor = new CertificateXmlEncryptor(
                            dataProtection.CertificateThumbprint,
                            new CertificateResolver(),
                            loggerFactory));
        }

        services.AddSingleton<IMfaSecretProtector, MfaSecretProtector>();
        OptionsBuilder<SecurityKeySettings> securityKeyOptions = services
            .AddOptions<SecurityKeySettings>()
            .Bind(configuration.GetSection(SecurityKeySettings.SectionName));
        if (environment.IsProduction())
        {
            securityKeyOptions.Validate(
                SecurityKeySettings.HasProductionKey,
                "Production requires a versioned Base64 challenge HMAC key of at least 256 bits.");
        }

        securityKeyOptions.ValidateOnStart();
        services.AddSingleton<ISecurityKeyProvider, ConfigurationSecurityKeyProvider>();
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiAuthenticationSchemes.Session;
                options.DefaultChallengeScheme = ApiAuthenticationSchemes.Session;
                options.DefaultSignInScheme = ApiAuthenticationSchemes.Session;
            })
            .AddCookie(
                ApiAuthenticationSchemes.Session,
                options => ConfigureCookie(
                    options,
                    ApiAuthenticationSchemes.SessionCookieName,
                    TimeSpan.FromMinutes(30),
                    useSlidingExpiration: true))
            .AddCookie(
                ApiAuthenticationSchemes.Recovery,
                options =>
                {
                    ConfigureCookie(
                        options,
                        ApiAuthenticationSchemes.RecoveryCookieName,
                        TimeSpan.FromMinutes(15),
                        useSlidingExpiration: false);
                    options.Events.OnValidatePrincipal =
                        ValidateRecoveryPrincipalAsync;
                });
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder(ApiAuthenticationSchemes.Session)
                    .RequireAuthenticatedUser()
                    .Build())
            .AddPolicy(
                "RecoveryOnly",
                policy => policy
                    .AddAuthenticationSchemes(
                        ApiAuthenticationSchemes.Recovery)
                    .RequireAuthenticatedUser());
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-AppCore.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IActorContext, HttpContextActorContext>();
        services.AddSingleton<SensitiveDataRedactor>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] =
                    context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions["correlationId"] =
                    context.HttpContext.TraceIdentifier;
            };
        });

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
            });

        services.AddOpenApi("v1");
        services.AddCors(options =>
        {
            options.AddPolicy(
                CorsSettings.PolicyName,
                policy => policy
                    .WithOrigins(cors.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)
                    .AllowCredentials());
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                RateLimitingPolicyNames.Sensitive,
                context => RateLimitingPolicies.CreateSensitivePartition(
                    context,
                    rateLimiting));
            options.OnRejected = async (context, cancellationToken) =>
            {
                IProblemDetailsService problemDetails =
                    context.HttpContext.RequestServices
                        .GetRequiredService<IProblemDetailsService>();
                context.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;
                await problemDetails.TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests.",
                            Type = ProblemTypes.TooManyRequests,
                        },
                    });
            };
        });

        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
                tags: ["live"]);

        services.AddOptions<BrandingStorageOptions>()
            .Bind(configuration.GetSection(BrandingStorageOptions.SectionName));
        services.AddInfrastructure();

        return services;
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        string cookieName,
        TimeSpan lifetime,
        bool useSlidingExpiration)
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = lifetime;
        options.SlidingExpiration = useSlidingExpiration;
        options.LoginPath = PathString.Empty;
        options.AccessDeniedPath = PathString.Empty;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    private static async Task ValidateRecoveryPrincipalAsync(
        CookieValidatePrincipalContext context)
    {
        string? claim = context.Principal?.FindFirstValue(
            ApiAuthenticationSchemes.RecoverySessionIdClaim);
        string? userClaim = context.Principal?.FindFirstValue(
            ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out Guid recoverySessionId)
            || !Guid.TryParse(userClaim, out Guid userId))
        {
            context.RejectPrincipal();
            return;
        }

        ApplicationDbContext database =
            context.HttpContext.RequestServices
                .GetRequiredService<ApplicationDbContext>();
        bool active = await database.Database
            .SqlQuery<int>(
                $"""
                SELECT 1 AS "Value"
                FROM security.restricted_recovery_sessions AS r
                INNER JOIN identity.users AS u ON u."Id" = r."UserId"
                WHERE r."Id" = {recoverySessionId}
                  AND r."UserId" = {userId}
                  AND r."RevokedAtUtc" IS NULL
                  AND r."ExpiresAtUtc" > statement_timestamp()
                  AND u."MfaState" = 'RecoveryPending'
                  AND u."AccountStatus" = 'Enabled'
                  AND u."CredentialStatus" = 'Active'
                """)
            .AnyAsync(context.HttpContext.RequestAborted);
        if (!active)
        {
            context.RejectPrincipal();
        }
    }

    private static IPAddress[] ParseKnownProxies(IEnumerable<string> proxies)
    {
        var parsed = new List<IPAddress>();
        foreach (string proxy in proxies)
        {
            if (!IPAddress.TryParse(proxy, out IPAddress? address))
            {
                throw new OptionsValidationException(
                    ForwardedHeadersSettings.SectionName,
                    typeof(ForwardedHeadersSettings),
                    [$"ForwardedHeaders:KnownProxies contains an invalid IP address: {proxy}"]);
            }

            parsed.Add(address);
        }

        return [.. parsed];
    }

    private static bool IsExplicitHostName(string host)
    {
        if (host.Contains('*')
            || host.Contains("://", StringComparison.Ordinal)
            || host.Contains('/')
            || host.Contains('\\'))
        {
            return false;
        }

        string addressCandidate = host;
        if (host.StartsWith('[')
            && host.EndsWith(']'))
        {
            addressCandidate = host[1..^1];
        }
        else if (host.Contains(':'))
        {
            return false;
        }

        if (IPAddress.TryParse(addressCandidate, out IPAddress? address))
        {
            return !address.Equals(IPAddress.Any)
                && !address.Equals(IPAddress.IPv6Any);
        }

        return Uri.CheckHostName(host) == UriHostNameType.Dns;
    }

    private static void ValidateAllowedOrigins(IEnumerable<string> origins)
    {
        string[] configuredOrigins = origins.ToArray();

        if (configuredOrigins.Length == 0
            || configuredOrigins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
                || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath is not ("" or "/")
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.Host == "*"))
        {
            throw new OptionsValidationException(
                CorsSettings.SectionName,
                typeof(CorsSettings),
                ["CORS origins must be explicit HTTP or HTTPS origins without paths."]);
        }
    }
}
