namespace AppCore.Application.Security;

public interface ISessionValidator
{
    Task<ValidatedSession?> ValidateAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<bool> TouchAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<bool> RecheckAsync(
        Guid sessionId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);
}

public sealed record ValidatedSession(
    Guid SessionId,
    Guid UserId,
    long AuthorizationVersion,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset? MfaVerifiedAtUtc,
    string AuthenticationMethods);
