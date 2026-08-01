using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppCore.Infrastructure.Security;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(value => value.IsBuiltIn).HasDefaultValue(false);
        builder.Property(value => value.IsProtected).HasDefaultValue(false);
        builder.Property(value => value.IsArchived).HasDefaultValue(false);

        builder.HasData(AuthorizationSeed.Roles.Select(role => new ApplicationRole
        {
            Id = role.Id,
            Name = role.Name,
            NormalizedName = role.Name.ToUpperInvariant(),
            ConcurrencyStamp = $"phase-04c-{role.Id:N}",
            IsBuiltIn = true,
            IsProtected = role.IsProtected,
            IsArchived = false,
        }));
    }
}

public sealed class PermissionRecordConfiguration : IEntityTypeConfiguration<PermissionRecord>
{
    public void Configure(EntityTypeBuilder<PermissionRecord> builder)
    {
        builder.ToTable("permissions", DatabaseSchemas.Security);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasMaxLength(128);
        builder.Property(value => value.Assurance).HasMaxLength(32);
        builder.Property(value => value.Scope).HasMaxLength(32);
        builder.HasData(SystemPermissions.Catalog.Select(permission => new PermissionRecord
        {
            Id = permission.Id,
            Assurance = permission.Assurance.ToString(),
            Scope = permission.Scope.ToString(),
        }));
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionAssignment>
{
    public void Configure(EntityTypeBuilder<RolePermissionAssignment> builder)
    {
        builder.ToTable("role_permissions", DatabaseSchemas.Security);
        builder.HasKey(value => new { value.RoleId, value.PermissionId });
        builder.Property(value => value.PermissionId).HasMaxLength(128);
        builder.HasOne(value => value.Role)
            .WithMany()
            .HasForeignKey(value => value.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PermissionRecord>()
            .WithMany()
            .HasForeignKey(value => value.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            AuthorizationSeed.Roles.SelectMany(role =>
                role.Permissions.Select(permission => new RolePermissionAssignment
                {
                    RoleId = role.Id,
                    PermissionId = permission,
                })));
    }
}
