using AppCore.Infrastructure.Health;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class DatabaseMigrationTests(
    PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task InitialMigrationAppliesToPostgreSql()
    {
        await using ApplicationDbContext context = CreateContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        await context.Database.MigrateAsync(timeout.Token);
        string[] pendingMigrations =
            (await context.Database.GetPendingMigrationsAsync(timeout.Token))
            .ToArray();

        Assert.Empty(pendingMigrations);
        Assert.Contains(
            "20260726100125_InitialFoundation",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.Contains(
            "20260726175242_Phase04AIdentitySessionFoundation",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.Contains(
            "20260726182630_HardenPhase04ASecurityInvariants",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.Contains(
            "20260726190000_BeginPhase04BPreflight",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.Contains(
            "20260726210942_ImplementPhase04BAuthentication",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.Contains(
            "20260727082450_ClosePhase04BSecurityGaps",
            await context.Database.GetAppliedMigrationsAsync(timeout.Token));
        Assert.True(await SchemaExistsAsync("app", timeout.Token));
        Assert.True(await SchemaExistsAsync("identity", timeout.Token));
        Assert.True(await SchemaExistsAsync("security", timeout.Token));
        Assert.True(await SchemaExistsAsync("infrastructure", timeout.Token));
        BootstrapProgress bootstrap =
            await context.BootstrapProgress.SingleAsync(timeout.Token);
        Assert.Equal(BootstrapState.NotStarted, bootstrap.State);
        Assert.Null(bootstrap.ProtectedOwnerUserId);
    }

    [Fact]
    public async Task PostgreSqlHealthCheckReportsHealthy()
    {
        var healthCheck = new PostgreSqlHealthCheck(
            Options.Create(
                new DatabaseOptions
                {
                    ConnectionString = database.ConnectionString,
                }));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "postgresql",
                healthCheck,
                HealthStatus.Unhealthy,
                ["ready", "database"]),
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        HealthCheckResult result =
            await healthCheck.CheckHealthAsync(context, timeout.Token);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseNpgsql(
            database.ConnectionString,
            npgsql => npgsql
                .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                .MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    DatabaseSchemas.Infrastructure));

        return new ApplicationDbContext(options.Options);
    }

    private async Task<bool> SchemaExistsAsync(
        string schemaName,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.schemata
                WHERE schema_name = $1
            );
            """;
        _ = command.Parameters.AddWithValue(schemaName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? false);
    }
}
