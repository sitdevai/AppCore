using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppCore.Infrastructure.Security;

public sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.AccountStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(user => user.CredentialStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(user => user.MfaState).HasConversion<string>().HasMaxLength(32);
        builder.Property(user => user.AuthorizationVersion).IsConcurrencyToken();
        builder.Property(user => user.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(user => user.TemporarilyThrottledUntilUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(user => user.FailedLoginWindowStartedAtUtc)
            .HasColumnType("timestamp with time zone");
    }
}

public sealed class PasswordHistoryEntryConfiguration
    : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.ToTable("password_history", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.PasswordHash).HasMaxLength(512);
        builder.HasIndex(value => new { value.UserId, value.CreatedAtUtc });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ServerSessionConfiguration
    : IEntityTypeConfiguration<ServerSession>
{
    public void Configure(EntityTypeBuilder<ServerSession> builder)
    {
        builder.ToTable("sessions", DatabaseSchemas.Security);
        builder.HasKey(session => session.Id);
        builder.Property(session => session.AuthenticationMethods).HasMaxLength(128);
        builder.Property(session => session.DeviceLabel).HasMaxLength(128);
        builder.Property(session => session.ClientCategory).HasMaxLength(32);
        builder.HasIndex(session => new { session.UserId, session.RevokedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AnonymousPreSessionConfiguration
    : IEntityTypeConfiguration<AnonymousPreSession>
{
    public void Configure(EntityTypeBuilder<AnonymousPreSession> builder)
    {
        builder.ToTable("anonymous_pre_sessions", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.ExpiresAtUtc);
    }
}

public sealed class SecurityChallengeConfiguration
    : IEntityTypeConfiguration<SecurityChallenge>
{
    public void Configure(EntityTypeBuilder<SecurityChallenge> builder)
    {
        builder.ToTable("security_challenges", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Purpose).HasConversion<string>().HasMaxLength(48);
        builder.Property(value => value.KeyedHash).HasMaxLength(64);
        builder.HasIndex(value => new { value.UserId, value.Purpose })
            .IsUnique()
            .HasFilter("\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MfaLoginChallengeConfiguration
    : IEntityTypeConfiguration<MfaLoginChallenge>
{
    public void Configure(EntityTypeBuilder<MfaLoginChallenge> builder)
    {
        builder.ToTable("mfa_login_challenges", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.UserId)
            .IsUnique()
            .HasFilter("\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnonymousPreSession>().WithMany()
            .HasForeignKey(value => value.AnonymousPreSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MfaAuthenticator>().WithMany()
            .HasForeignKey(value => value.AuthenticatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MfaAuthenticatorConfiguration
    : IEntityTypeConfiguration<MfaAuthenticator>
{
    public void Configure(EntityTypeBuilder<MfaAuthenticator> builder)
    {
        builder.ToTable("mfa_authenticators", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.ProtectedSecret).HasMaxLength(1024);
        builder.HasIndex(value => value.UserId)
            .IsUnique()
            .HasFilter("\"RevokedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MfaRecoveryCodeConfiguration
    : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_codes", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.KeyedHash).HasMaxLength(64);
        builder.HasIndex(value => new { value.UserId, value.ConsumedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RestrictedRecoverySessionConfiguration
    : IEntityTypeConfiguration<RestrictedRecoverySession>
{
    public void Configure(EntityTypeBuilder<RestrictedRecoverySession> builder)
    {
        builder.ToTable("restricted_recovery_sessions", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.UserId)
            .IsUnique()
            .HasFilter("\"RevokedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BootstrapProgressConfiguration
    : IEntityTypeConfiguration<BootstrapProgress>
{
    public void Configure(EntityTypeBuilder<BootstrapProgress> builder)
    {
        builder.ToTable(
            "bootstrap_progress",
            DatabaseSchemas.Security,
            table => table.HasCheckConstraint(
                "CK_bootstrap_progress_singleton",
                "\"Id\" = 1"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.State).HasConversion<string>().HasMaxLength(32);
        builder.HasData(
            new BootstrapProgress
            {
                Id = 1,
                State = BootstrapState.NotStarted,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            });
    }
}

public sealed class SecurityAuditEventConfiguration
    : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("security_audit_events", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.EventCode).HasMaxLength(128);
        builder.Property(value => value.ResultCode).HasMaxLength(64);
        builder.Property(value => value.CorrelationId).HasMaxLength(128);
        builder.Property(value => value.DetailsJson).HasColumnType("jsonb");
        builder.HasIndex(value => value.OccurredAtUtc);
        builder.HasIndex(value => value.ActorUserId);
        builder.HasIndex(value => value.TargetUserId);
    }
}

public sealed class SecurityAuditContextConfiguration
    : IEntityTypeConfiguration<SecurityAuditContext>
{
    public void Configure(EntityTypeBuilder<SecurityAuditContext> builder)
    {
        builder.ToTable("security_audit_contexts", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.SourceIp).HasMaxLength(64);
        builder.Property(value => value.UserAgent).HasMaxLength(512);
        builder.HasIndex(value => value.SecurityAuditEventId).IsUnique();
        builder.HasIndex(value => value.ExpiresAtUtc);
        builder.HasOne(value => value.SecurityAuditEvent)
            .WithOne()
            .HasForeignKey<SecurityAuditContext>(
                value => value.SecurityAuditEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
