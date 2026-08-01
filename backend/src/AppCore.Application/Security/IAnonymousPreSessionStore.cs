namespace AppCore.Application.Security;

public interface IAnonymousPreSessionStore
{
    Task<Guid> CreateAsync(
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<bool> ConsumeAsync(
        Guid preSessionId,
        CancellationToken cancellationToken = default);
}
