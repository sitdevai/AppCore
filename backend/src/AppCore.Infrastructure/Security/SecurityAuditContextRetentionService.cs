using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class SecurityAuditContextRetentionService(
    ApplicationDbContext context)
{
    public async Task<int> DeleteExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset databaseNow = await context.Database
            .SqlQuery<DateTimeOffset>(
                $"SELECT statement_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        return await context.SecurityAuditContexts
            .Where(value => value.ExpiresAtUtc <= databaseNow)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
