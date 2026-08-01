using System.Text.Json;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;

namespace AppCore.Infrastructure.Security;

public sealed class SecurityAuditWriter(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : ISecurityAuditWriter
{
    private static readonly HashSet<string> AllowedDetailKeys =
    [
        "reason",
        "revokedCount",
        "roleId",
    ];
    private static readonly HashSet<string> AllowedReasonCodes =
    [
        "concurrent_limit",
    ];
    private const int MaximumDetailCount = 8;
    private const int MaximumDetailValueLength = 256;

    public async Task WriteAsync(
        SecurityAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!SecurityAuditCodes.IsSafe(entry.EventCode)
            || !SecurityAuditCodes.IsSafe(entry.ResultCode)
            || entry.ResultCode.Length > 64)
        {
            throw new ArgumentException(
                "Audit event and result codes must be stable safe identifiers.",
                nameof(entry));
        }

        if (entry.Details?.Count > MaximumDetailCount
            || entry.Details?.Keys.Any(key => !AllowedDetailKeys.Contains(key)) == true)
        {
            throw new ArgumentException(
                "Audit details contain an unapproved key or exceed the limit.",
                nameof(entry));
        }

        ValidateDetailValues(entry.Details);

        Dictionary<string, string?>? safeDetails = entry.Details?
            .ToDictionary(
                pair => pair.Key,
                pair => Sanitize(pair.Value, MaximumDetailValueLength),
                StringComparer.Ordinal);

        var auditEvent = new SecurityAuditEvent(
            entry.EventCode,
            entry.ResultCode,
            timeProvider.GetUtcNow(),
            entry.ActorUserId,
            entry.TargetUserId,
            string.IsNullOrWhiteSpace(entry.CorrelationId)
                ? "unavailable"
                : Sanitize(entry.CorrelationId, 128)!,
            safeDetails is null ? null : JsonSerializer.Serialize(safeDetails));

        context.SecurityAuditEvents.Add(auditEvent);
        if (!string.IsNullOrWhiteSpace(entry.SourceIp)
            || !string.IsNullOrWhiteSpace(entry.UserAgent))
        {
            context.SecurityAuditContexts.Add(
                new SecurityAuditContext
                {
                    SecurityAuditEvent = auditEvent,
                    SourceIp = Sanitize(entry.SourceIp, 64),
                    UserAgent = Sanitize(entry.UserAgent, 512),
                    ExpiresAtUtc = timeProvider.GetUtcNow().AddDays(90),
                });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDetailValues(
        IReadOnlyDictionary<string, string?>? details)
    {
        if (details is null)
        {
            return;
        }

        foreach ((string key, string? value) in details)
        {
            bool valid = key switch
            {
                "reason" => value is not null
                    && AllowedReasonCodes.Contains(value),
                "revokedCount" => int.TryParse(
                    value,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int count) && count >= 0,
                "roleId" => Guid.TryParse(value, out _),
                _ => false,
            };
            if (!valid)
            {
                throw new ArgumentException(
                    "Audit detail values must use approved typed codes.",
                    nameof(details));
            }
        }
    }

    private static string? Sanitize(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string sanitized = string.Concat(
            value.Select(character =>
                char.IsControl(character) ? ' ' : character));
        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength];
    }
}
