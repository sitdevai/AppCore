namespace AppCore.Application.Security;

public interface ISecurityKeyProvider
{
    ValueTask<VersionedSecurityKey> GetCurrentKeyAsync(
        string purpose,
        CancellationToken cancellationToken = default);

    ValueTask<VersionedSecurityKey?> GetKeyAsync(
        string purpose,
        int version,
        CancellationToken cancellationToken = default);
}

public sealed record VersionedSecurityKey(int Version, ReadOnlyMemory<byte> Key);
