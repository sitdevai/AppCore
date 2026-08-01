using System.Text;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class PasswordPolicyService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager)
    : IPasswordPolicyService
{
    private static readonly Lazy<HashSet<string>> BlockedPasswords =
        new(LoadOfflineBlocklist);

    public async Task<string?> NormalizeAndValidateAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        string normalized = password.Normalize(NormalizationForm.FormC);
        if (user is null
            || normalized.Length is < 15 or > 128
            || BlockedPasswords.Value.Contains(normalized)
            || normalized.All(character => character == normalized[0])
            || normalized.Contains(user.UserName!, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] hashes = await context.PasswordHistory
            .Where(value => value.UserId == user.Id)
            .OrderByDescending(value => value.CreatedAtUtc)
            .Take(5)
            .Select(value => value.PasswordHash)
            .ToArrayAsync(cancellationToken);
        if (user.PasswordHash is not null)
        {
            hashes = [user.PasswordHash, .. hashes.Take(5)];
        }

        return hashes.All(hash =>
            userManager.PasswordHasher.VerifyHashedPassword(
                user,
                hash,
                normalized) == PasswordVerificationResult.Failed)
            ? normalized
            : null;
    }

    private static HashSet<string> LoadOfflineBlocklist()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Security",
            "compromised-passwords.txt");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The offline compromised-password blocklist is missing.");
        }

        return File.ReadLines(path)
            .Select(value => value.Trim().Normalize(NormalizationForm.FormC))
            .Where(value => value.Length > 0 && !value.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
