using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class OptionalUniqueEmailUserValidator(
    IdentityErrorDescriber errors)
    : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        string? email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
        {
            return IdentityResult.Success;
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return IdentityResult.Failed(errors.InvalidEmail(email));
        }

        string? normalizedEmail = manager.NormalizeEmail(email);
        bool duplicate = await manager.Users.AnyAsync(candidate =>
            candidate.Id != user.Id
            && candidate.NormalizedEmail == normalizedEmail);

        return duplicate
            ? IdentityResult.Failed(errors.DuplicateEmail(email))
            : IdentityResult.Success;
    }
}
