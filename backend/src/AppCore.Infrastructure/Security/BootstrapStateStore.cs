using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class BootstrapStateStore(
    ApplicationDbContext context,
    TimeProvider timeProvider)
{
    public async Task<bool> AdvanceAsync(
        BootstrapState expectedState,
        BootstrapState nextState,
        Guid? protectedOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if ((int)nextState != (int)expectedState + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextState),
                "Bootstrap state may advance by exactly one state.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.BootstrapProgress
            .Where(progress =>
                progress.Id == 1
                && progress.State == expectedState
                && (progress.ProtectedOwnerUserId == null
                    || progress.ProtectedOwnerUserId == protectedOwnerUserId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(progress => progress.State, nextState)
                    .SetProperty(
                        progress => progress.ProtectedOwnerUserId,
                        protectedOwnerUserId)
                    .SetProperty(progress => progress.UpdatedAtUtc, now)
                    .SetProperty(
                        progress => progress.CompletedAtUtc,
                        nextState == BootstrapState.Completed ? now : null),
                cancellationToken);
        return affected == 1;
    }
}
