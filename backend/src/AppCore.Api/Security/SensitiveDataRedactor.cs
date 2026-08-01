using AppCore.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AppCore.Api.Security;

public sealed class SensitiveDataRedactor(
    IOptions<LoggingRedactionSettings> settings)
{
    public const string RedactedValue = "[REDACTED]";

    public string? Redact(string key, string? value)
    {
        bool isSensitive = settings.Value.SensitiveKeys.Any(
            sensitiveKey => key.Contains(
                sensitiveKey,
                StringComparison.OrdinalIgnoreCase));

        return isSensitive ? RedactedValue : value;
    }
}
