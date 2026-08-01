using System.Threading.RateLimiting;
using AppCore.Api.Configuration;

namespace AppCore.Api.RateLimiting;

public static class RateLimitingPolicies
{
    public static RateLimitPartition<string> CreateSensitivePartition(
        HttpContext context,
        RateLimitingSettings settings) =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeyResolver.Resolve(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.SensitivePermitLimit,
                Window = TimeSpan.FromSeconds(
                    settings.SensitiveWindowSeconds),
                QueueLimit = settings.SensitiveQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
}
