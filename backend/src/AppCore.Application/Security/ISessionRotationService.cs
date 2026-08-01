namespace AppCore.Application.Security;

public interface ISessionRotationService
{
    Task<Guid> RotateAsync(
        Guid userId,
        Guid? priorSessionId,
        long authorizationVersion,
        DateTimeOffset? mfaVerifiedAtUtc,
        string authenticationMethods,
        CancellationToken cancellationToken = default);
}
