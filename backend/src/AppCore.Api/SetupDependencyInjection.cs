using AppCore.Api.Configuration;
using AppCore.Api.Security;
using AppCore.Application.Common.Abstractions;
using AppCore.Application.Security;
using AppCore.Infrastructure;
using AppCore.Infrastructure.Persistence;

namespace AppCore.Api;

public static class SetupDependencyInjection
{
    public static IServiceCollection AddSetupServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                DatabaseOptions.HasValidConnectionString,
                "Database:ConnectionString must be a valid PostgreSQL connection string.")
            .ValidateOnStart();
        services
            .AddOptions<SecurityKeySettings>()
            .Bind(configuration.GetSection(SecurityKeySettings.SectionName))
            .Validate(
                SecurityKeySettings.HasProductionKey,
                "Setup requires the same versioned SecurityKeys key ring used by the API.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<
            ISecurityKeyProvider,
            ConfigurationSecurityKeyProvider>();
        services.AddScoped<IActorContext, SetupActorContext>();
        services.AddInfrastructure();
        return services;
    }

    private sealed class SetupActorContext : IActorContext
    {
        public string ActorId => "trusted-setup-console";
    }
}
