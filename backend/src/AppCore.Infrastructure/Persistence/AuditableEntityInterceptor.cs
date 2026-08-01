using AppCore.Application.Common.Abstractions;
using AppCore.Domain.Common.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppCore.Infrastructure.Persistence;

public sealed class AuditableEntityInterceptor(
    TimeProvider timeProvider,
    IActorContext actorContext)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        string? actorId = actorContext.ActorId;

        foreach (EntityEntry<IHasCreationAudit> entry in
                 context.ChangeTracker.Entries<IHasCreationAudit>())
        {
            if (entry.State == EntityState.Modified)
            {
                var createdAt = entry.Property<DateTime>(
                    nameof(IHasCreationAudit.CreatedAtUtc));
                var createdBy = entry.Property<string?>(
                    nameof(IHasCreationAudit.CreatedByActorId));
                createdAt.CurrentValue = createdAt.OriginalValue;
                createdBy.CurrentValue = createdBy.OriginalValue;
                createdAt.IsModified = false;
                createdBy.IsModified = false;
                continue;
            }

            if (entry.State != EntityState.Added)
            {
                continue;
            }

            entry.Entity.CreatedAtUtc = utcNow;
            entry.Entity.CreatedByActorId = actorId;
        }

        foreach (EntityEntry<IHasModificationAudit> entry in
                 context.ChangeTracker.Entries<IHasModificationAudit>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.LastModifiedAtUtc = utcNow;
            entry.Entity.LastModifiedByActorId = actorId;
        }
    }
}
