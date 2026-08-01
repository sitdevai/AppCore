using System.Security.Cryptography;
using AppCore.Api.Configuration;
using AppCore.Application.Security;
using Microsoft.Extensions.Options;

namespace AppCore.Api.Security;

public sealed class ConfigurationSecurityKeyProvider
    : ISecurityKeyProvider
{
    private readonly Dictionary<int, VersionedSecurityKey> keys;
    private readonly int currentVersion;

    public ConfigurationSecurityKeyProvider(
        IOptions<SecurityKeySettings> settings,
        IHostEnvironment environment)
    {
        SecurityKeySettings value = settings.Value;
        currentVersion = value.CurrentVersion > 0
            ? value.CurrentVersion
            : value.ChallengeHmacKeyVersion;
        Dictionary<int, string> configured = value.Keys.Count > 0
            ? value.Keys
            : string.IsNullOrWhiteSpace(value.ChallengeHmacKeyBase64)
                ? []
                : new Dictionary<int, string>
                {
                    [value.ChallengeHmacKeyVersion] =
                        value.ChallengeHmacKeyBase64,
                };
        if (configured.Count == 0 && environment.IsDevelopment())
        {
            configured[1] = Convert.ToBase64String(
                SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        "AppCore.Development.ChallengeHmacKey")));
            currentVersion = 1;
        }

        keys = configured.ToDictionary(
            value => value.Key,
            value => new VersionedSecurityKey(
                value.Key,
                Convert.FromBase64String(value.Value)));
        if (!keys.ContainsKey(currentVersion))
        {
            throw new InvalidOperationException(
                "The current challenge HMAC key version is not configured.");
        }
    }

    public ValueTask<VersionedSecurityKey> GetCurrentKeyAsync(
        string purpose,
        CancellationToken cancellationToken = default)
    {
        EnsurePurpose(purpose);
        return ValueTask.FromResult(keys[currentVersion]);
    }

    public ValueTask<VersionedSecurityKey?> GetKeyAsync(
        string purpose,
        int version,
        CancellationToken cancellationToken = default)
    {
        EnsurePurpose(purpose);
        return ValueTask.FromResult<VersionedSecurityKey?>(
            keys.GetValueOrDefault(version));
    }

    private static void EnsurePurpose(string purpose)
    {
        if (!string.Equals(
                purpose,
                "challenge-hmac",
                StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }
    }
}
