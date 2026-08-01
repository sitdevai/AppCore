using AppCore.Application.Branding;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppCore.Infrastructure.Branding;

public sealed class VisualIdentitySettingsConfiguration
    : IEntityTypeConfiguration<VisualIdentitySettings>
{
    public void Configure(EntityTypeBuilder<VisualIdentitySettings> builder)
    {
        builder.ToTable("visual_identity_settings", DatabaseSchemas.Application,
            table => table.HasCheckConstraint("CK_visual_identity_singleton", "\"Id\" = 1"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.OrganizationName).HasMaxLength(200);
        builder.Property(value => value.ShortOrganizationName).HasMaxLength(80);
        builder.Property(value => value.PrimaryColor).HasMaxLength(7);
        builder.Property(value => value.SecondaryColor).HasMaxLength(7);
        builder.Property(value => value.HeaderColor).HasMaxLength(7);
        builder.Property(value => value.BackgroundColor).HasMaxLength(7);
        builder.Property(value => value.PatternColor).HasMaxLength(7);
        builder.Property(value => value.BackgroundPattern).HasConversion<string>().HasMaxLength(16);
        builder.Property(value => value.Version).IsConcurrencyToken();
        builder.HasData(new VisualIdentitySettings
        {
            Id = 1,
            OrganizationName = BrandingDefaults.OrganizationName,
            ShortOrganizationName = BrandingDefaults.ShortOrganizationName,
            PrimaryColor = BrandingDefaults.PrimaryColor,
            SecondaryColor = BrandingDefaults.SecondaryColor,
            HeaderColor = BrandingDefaults.HeaderColor,
            BackgroundColor = BrandingDefaults.BackgroundColor,
            PatternColor = BrandingDefaults.PatternColor,
            BackgroundPattern = BrandingDefaults.BackgroundPattern,
            Version = 1,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        });
    }
}

public sealed class BrandingAssetConfiguration : IEntityTypeConfiguration<BrandingAsset>
{
    public void Configure(EntityTypeBuilder<BrandingAsset> builder)
    {
        builder.ToTable("branding_assets", DatabaseSchemas.Application);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.OriginalFileName).HasMaxLength(255);
        builder.Property(value => value.StoredFileName).HasMaxLength(80);
        builder.Property(value => value.ContentType).HasMaxLength(64);
        builder.HasIndex(value => value.StoredFileName).IsUnique();
    }
}
