using System.ComponentModel.DataAnnotations;

namespace AppCore.Api.Configuration;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10_000)]
    public int SensitivePermitLimit { get; init; } = 10;

    [Range(1, 3_600)]
    public int SensitiveWindowSeconds { get; init; } = 60;

    [Range(0, 10_000)]
    public int SensitiveQueueLimit { get; init; }
}
