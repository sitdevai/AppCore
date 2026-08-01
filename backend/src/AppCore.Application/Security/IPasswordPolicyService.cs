namespace AppCore.Application.Security;

public interface IPasswordPolicyService
{
    Task<string?> NormalizeAndValidateAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default);
}
