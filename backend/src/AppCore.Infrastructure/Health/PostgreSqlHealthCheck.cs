using AppCore.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AppCore.Infrastructure.Health;

public sealed class PostgreSqlHealthCheck(
    IOptions<DatabaseOptions> databaseOptions)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                new NpgsqlConnection(databaseOptions.Value.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception)
            when (exception is
                ArgumentException
                or InvalidOperationException
                or NpgsqlException
                or TimeoutException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL is unavailable.");
        }
    }
}
