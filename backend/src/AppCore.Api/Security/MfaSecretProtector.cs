using AppCore.Application.Security;
using Microsoft.AspNetCore.DataProtection;

namespace AppCore.Api.Security;

public sealed class MfaSecretProtector(IDataProtectionProvider provider)
    : IMfaSecretProtector
{
    private readonly IDataProtector protector =
        provider.CreateProtector("AppCore.Security.MfaSecret.v1");

    public byte[] Protect(ReadOnlySpan<byte> secret) =>
        protector.Protect(secret.ToArray());

    public byte[] Unprotect(ReadOnlySpan<byte> protectedSecret) =>
        protector.Unprotect(protectedSecret.ToArray());
}
