using System.Data;
using System.Data.Common;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppCore.Infrastructure.Security;

public sealed class ServerSessionValidator(ApplicationDbContext context)
    : ISessionValidator
{
    private const string ValidateSql =
        """
        SELECT s."Id", s."UserId", s."AuthorizationVersion",
               s."AbsoluteExpiresAtUtc", s."LastActivityAtUtc",
               s."MfaVerifiedAtUtc", s."AuthenticationMethods"
        FROM security.sessions AS s
        INNER JOIN identity.users AS u ON u."Id" = s."UserId"
        CROSS JOIN LATERAL (SELECT statement_timestamp() AS db_now) AS clock
        WHERE s."Id" = @session_id
          AND u."AccountStatus" = 'Enabled'
          AND u."CredentialStatus" = 'Active'
          AND u."AuthorizationVersion" = @expected_version
          AND s."RevokedAtUtc" IS NULL
          AND s."AuthorizationVersion" = @expected_version
          AND s."AbsoluteExpiresAtUtc" > clock.db_now
          AND s."LastActivityAtUtc" + INTERVAL '30 minutes' > clock.db_now
        ;
        """;

    private const string TouchSql =
        """
        WITH validation_clock AS (SELECT statement_timestamp() AS db_now)
        UPDATE security.sessions AS s
        SET "LastActivityAtUtc" = GREATEST(s."LastActivityAtUtc", clock.db_now)
        FROM validation_clock AS clock, identity.users AS u
        WHERE s."Id" = @session_id
          AND u."Id" = s."UserId"
          AND u."AccountStatus" = 'Enabled'
          AND u."CredentialStatus" = 'Active'
          AND u."AuthorizationVersion" = @expected_version
          AND s."RevokedAtUtc" IS NULL
          AND s."AuthorizationVersion" = @expected_version
          AND s."AbsoluteExpiresAtUtc" > clock.db_now
          AND s."LastActivityAtUtc" + INTERVAL '30 minutes' > clock.db_now;
        """;

    private const string RecheckSql =
        """
        SELECT 1
        FROM security.sessions AS s
        INNER JOIN identity.users AS u ON u."Id" = s."UserId"
        CROSS JOIN LATERAL (SELECT statement_timestamp() AS db_now) AS clock
        WHERE s."Id" = @session_id
          AND u."AccountStatus" = 'Enabled'
          AND u."CredentialStatus" = 'Active'
          AND u."AuthorizationVersion" = @expected_version
          AND s."RevokedAtUtc" IS NULL
          AND s."AuthorizationVersion" = @expected_version
          AND s."AbsoluteExpiresAtUtc" > clock.db_now
          AND s."LastActivityAtUtc" + INTERVAL '30 minutes' > clock.db_now
        FOR SHARE OF s, u;
        """;

    public async Task<ValidatedSession?> ValidateAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand command = await CreateCommandAsync(
            ValidateSql,
            sessionId,
            expectedAuthorizationVersion,
            cancellationToken);
        await using DbDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ValidatedSession(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetString(6));
    }

    public async Task<bool> TouchAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand command = await CreateCommandAsync(
            TouchSql,
            sessionId,
            expectedAuthorizationVersion,
            cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> RecheckAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Sensitive session recheck requires an active database transaction.");
        }

        await using DbCommand command = await CreateCommandAsync(
            RecheckSql,
            sessionId,
            expectedAuthorizationVersion,
            cancellationToken);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private async Task<DbCommand> CreateCommandAsync(
        string sql,
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        DbParameter sessionParameter = command.CreateParameter();
        sessionParameter.ParameterName = "session_id";
        sessionParameter.Value = sessionId;
        command.Parameters.Add(sessionParameter);

        DbParameter versionParameter = command.CreateParameter();
        versionParameter.ParameterName = "expected_version";
        versionParameter.Value = expectedAuthorizationVersion;
        command.Parameters.Add(versionParameter);

        return command;
    }
}
