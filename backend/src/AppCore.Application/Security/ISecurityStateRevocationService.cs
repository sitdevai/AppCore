namespace AppCore.Application.Security;

public interface ISecurityStateRevocationService
{
    Task RevokeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
