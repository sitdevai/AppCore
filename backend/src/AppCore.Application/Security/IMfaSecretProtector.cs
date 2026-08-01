namespace AppCore.Application.Security;

public interface IMfaSecretProtector
{
    byte[] Protect(ReadOnlySpan<byte> secret);
    byte[] Unprotect(ReadOnlySpan<byte> protectedSecret);
}
