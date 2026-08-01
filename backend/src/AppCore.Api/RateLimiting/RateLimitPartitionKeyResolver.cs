using System.Security.Claims;

namespace AppCore.Api.RateLimiting;

public static class RateLimitPartitionKeyResolver
{
    public static string Resolve(HttpContext context)
    {
        string? actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrWhiteSpace(actorId)
            ? $"actor:{actorId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
