using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppCore.Infrastructure.Security;

public sealed partial class SecurityAuditRetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SecurityAuditRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PurgeAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PurgeAsync(stoppingToken);
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            SecurityAuditContextRetentionService retention =
                scope.ServiceProvider.GetRequiredService<SecurityAuditContextRetentionService>();
            int deleted = await retention.DeleteExpiredAsync(cancellationToken);
            LogCompleted(logger, deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 4201, Level = LogLevel.Information,
        Message = "Security audit context retention completed; deleted {DeletedCount} expired records")]
    private static partial void LogCompleted(ILogger logger, int deletedCount);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Warning,
        Message = "Security audit context retention failed closed with {ExceptionType}")]
    private static partial void LogFailed(ILogger logger, string exceptionType);
}
