using AppCore.Infrastructure.Branding;
using AppCore.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RolePermissionAssignment> RolePermissions => Set<RolePermissionAssignment>();
    public DbSet<PermissionRecord> Permissions => Set<PermissionRecord>();
    public DbSet<ServerSession> ServerSessions => Set<ServerSession>();
    public DbSet<AnonymousPreSession> AnonymousPreSessions => Set<AnonymousPreSession>();
    public DbSet<SecurityChallenge> SecurityChallenges => Set<SecurityChallenge>();
    public DbSet<MfaLoginChallenge> MfaLoginChallenges => Set<MfaLoginChallenge>();
    public DbSet<MfaAuthenticator> MfaAuthenticators => Set<MfaAuthenticator>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();
    public DbSet<RestrictedRecoverySession> RestrictedRecoverySessions =>
        Set<RestrictedRecoverySession>();
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();
    public DbSet<BootstrapProgress> BootstrapProgress => Set<BootstrapProgress>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<SecurityAuditContext> SecurityAuditContexts =>
        Set<SecurityAuditContext>();
    public DbSet<VisualIdentitySettings> VisualIdentitySettings =>
        Set<VisualIdentitySettings>();
    public DbSet<BrandingAsset> BrandingAssets => Set<BrandingAsset>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(DatabaseSchemas.Application);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ConfigureIdentityTables(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectAuditMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectAuditMutations()
    {
        if (ChangeTracker.Entries<SecurityAuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Security audit events are append-only.");
        }
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("users", DatabaseSchemas.Identity);
        modelBuilder.Entity<ApplicationRole>().ToTable("roles", DatabaseSchemas.Identity);
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", DatabaseSchemas.Identity);
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", DatabaseSchemas.Identity);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", DatabaseSchemas.Identity);
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", DatabaseSchemas.Identity);
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", DatabaseSchemas.Identity);
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("\"NormalizedEmail\" IS NOT NULL");
    }
}
