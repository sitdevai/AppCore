using AppCore.Application.Branding;
using AppCore.Application.Security;
using AppCore.Infrastructure.Branding;
using AppCore.Infrastructure.Health;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 15;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddUserValidator<OptionalUniqueEmailUserValidator>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<ISecurityAuditWriter, SecurityAuditWriter>();
        services.AddScoped<ISessionValidator, ServerSessionValidator>();
        services.AddScoped<IAnonymousPreSessionStore, AnonymousPreSessionStore>();
        services.AddScoped<ISessionRotationService, SessionRotationService>();
        services.AddScoped<AtomicSecurityStateStore>();
        services.AddScoped<BootstrapStateStore>();
        services.AddScoped<SecurityAuditContextRetentionService>();
        services.AddHostedService<SecurityAuditRetentionWorker>();
        services.AddScoped<
            IAuthenticationWorkflowService,
            AuthenticationWorkflowService>();
        services.AddScoped<IAccountLifecycleService, AccountLifecycleService>();
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
        services.AddScoped<ISecurityStateRevocationService, SecurityStateRevocationService>();
        services.AddScoped<IMfaEnrollmentService, MfaEnrollmentService>();
        services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();
        services.AddScoped<IRoleAuthorizationService, RoleAuthorizationService>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<ISecurityAdministrationService, SecurityAdministrationService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddSingleton<IBrandingFileStore, LocalBrandingFileStore>();
        services.AddScoped<BootstrapIdentityPreparationService>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            DatabaseOptions database =
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            AuditableEntityInterceptor auditInterceptor =
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>();

            options
                .UseNpgsql(
                    database.ConnectionString,
                    npgsql => npgsql
                        .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                        .MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            DatabaseSchemas.Infrastructure))
                .AddInterceptors(auditInterceptor);
        });

        services
            .AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>(
                "postgresql",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready", "database"]);

        return services;
    }
}
