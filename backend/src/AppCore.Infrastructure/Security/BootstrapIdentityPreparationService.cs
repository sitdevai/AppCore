using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class BootstrapIdentityPreparationService(
    ApplicationDbContext context,
    IAccountLifecycleService lifecycle,
    BootstrapStateStore bootstrap,
    ISecurityAuditWriter auditWriter,
    ISecurityStateRevocationService revocation)
{
    public async Task<OneTimeChallengeResult> CreateOwnerAsync(
        string username,
        string? email,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM security.bootstrap_progress WHERE \"Id\" = 1 FOR UPDATE;",
            cancellationToken);
        BootstrapProgress state = await context.BootstrapProgress
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        if (state.State != BootstrapState.NotStarted)
        {
            throw new InvalidOperationException("Bootstrap owner already exists.");
        }

        AccountCreationResult owner = await lifecycle.CreateAsync(
            username,
            email,
            protectedOwner: true,
            cancellationToken);
        OneTimeChallengeResult challenge = await lifecycle.IssueChallengeAsync(
            owner.UserId,
            "activation",
            cancellationToken);
        if (!await bootstrap.AdvanceAsync(
                BootstrapState.NotStarted,
                BootstrapState.OwnerCreated,
                owner.UserId,
                cancellationToken))
        {
            throw new InvalidOperationException("Bootstrap state changed concurrently.");
        }

        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.BootstrapStateChanged,
                "success",
                TargetUserId: owner.UserId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return challenge;
    }

    public async Task<bool> EnablePreparedOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM security.bootstrap_progress WHERE \"Id\" = 1 FOR UPDATE;",
            cancellationToken);
        BootstrapProgress state = await context.BootstrapProgress
            .SingleAsync(cancellationToken);
        ApplicationUser? owner = await context.Users.SingleOrDefaultAsync(
            value => value.Id == ownerUserId && value.IsProtectedOwner,
            cancellationToken);
        if (state.State != BootstrapState.OwnerCreated
            || state.ProtectedOwnerUserId != ownerUserId
            || owner is null
            || owner.AccountStatus != AccountStatus.Disabled
            || owner.CredentialStatus != CredentialStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        owner.AccountStatus = AccountStatus.Enabled;
        owner.AuthorizationVersion++;
        owner.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.AccountStateChanged,
                "success",
                TargetUserId: ownerUserId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkReadyForPrivilegeGrantAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM security.bootstrap_progress WHERE \"Id\" = 1 FOR UPDATE;",
            cancellationToken);
        bool prepared = await context.Users.AnyAsync(
            value =>
                value.Id == ownerUserId
                && value.IsProtectedOwner
                && value.AccountStatus == AccountStatus.Enabled
                && value.CredentialStatus == CredentialStatus.Active
                && value.MfaState == MfaState.Active,
            cancellationToken);
        int verifiedAuthenticators = await context.MfaAuthenticators.CountAsync(
            value =>
                value.UserId == ownerUserId
                && value.RevokedAtUtc == null
                && value.VerifiedAtUtc != null,
            cancellationToken);
        bool advanced = prepared
            && verifiedAuthenticators == 1
            && await bootstrap.AdvanceAsync(
                BootstrapState.OwnerCreated,
                BootstrapState.ReadyForPrivilegeGrant,
                ownerUserId,
                cancellationToken);
        if (advanced)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return advanced;
    }

    public async Task<bool> CompletePrivilegeGrantAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM security.bootstrap_progress WHERE \"Id\" = 1 FOR UPDATE;",
            cancellationToken);
        BootstrapProgress state = await context.BootstrapProgress
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        ApplicationUser? owner = await context.Users.SingleOrDefaultAsync(
            value => value.Id == ownerUserId && value.IsProtectedOwner,
            cancellationToken);
        bool hasVerifiedMfa = await context.MfaAuthenticators.AnyAsync(
            value => value.UserId == ownerUserId
                && value.VerifiedAtUtc != null
                && value.RevokedAtUtc == null,
            cancellationToken);
        bool protectedRoleExists = await context.Roles.AnyAsync(
            value => value.Id == SystemRoleIds.SystemAdministrator
                && value.IsProtected
                && !value.IsArchived,
            cancellationToken);
        bool alreadyAssigned = await context.UserRoles.AnyAsync(
            value => value.RoleId == SystemRoleIds.SystemAdministrator,
            cancellationToken);
        if (state.State != BootstrapState.ReadyForPrivilegeGrant
            || state.ProtectedOwnerUserId != ownerUserId
            || owner is null
            || owner.AccountStatus != AccountStatus.Enabled
            || owner.CredentialStatus != CredentialStatus.Active
            || owner.MfaState != MfaState.Active
            || !hasVerifiedMfa
            || !protectedRoleExists
            || alreadyAssigned)
        {
            return false;
        }

        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = ownerUserId,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        owner.AuthorizationVersion++;
        owner.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await revocation.RevokeAsync(ownerUserId, cancellationToken);
        if (!await bootstrap.AdvanceAsync(
                BootstrapState.ReadyForPrivilegeGrant,
                BootstrapState.Completed,
                ownerUserId,
                cancellationToken))
        {
            return false;
        }

        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.BootstrapPrivilegeGranted,
                "success",
                TargetUserId: ownerUserId,
                Details: new Dictionary<string, string?>
                {
                    ["roleId"] = SystemRoleIds.SystemAdministrator.ToString("D"),
                }),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
