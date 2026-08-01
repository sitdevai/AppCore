using AppCore.Application.Branding;

namespace AppCore.Infrastructure.Branding;

public sealed class VisualIdentitySettings
{
    public int Id { get; set; } = 1;
    public string OrganizationName { get; set; } = BrandingDefaults.OrganizationName;
    public string ShortOrganizationName { get; set; } = BrandingDefaults.ShortOrganizationName;
    public string PrimaryColor { get; set; } = BrandingDefaults.PrimaryColor;
    public string SecondaryColor { get; set; } = BrandingDefaults.SecondaryColor;
    public string HeaderColor { get; set; } = BrandingDefaults.HeaderColor;
    public string BackgroundColor { get; set; } = BrandingDefaults.BackgroundColor;
    public string PatternColor { get; set; } = BrandingDefaults.PatternColor;
    public BrandingBackgroundPattern BackgroundPattern { get; set; } =
        BrandingDefaults.BackgroundPattern;
    public Guid? LightLogoAssetId { get; set; }
    public Guid? DarkLogoAssetId { get; set; }
    public Guid? CompactLogoAssetId { get; set; }
    public Guid? FaviconAssetId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class BrandingAsset
{
    public Guid Id { get; set; }
    public BrandingAssetKind Kind { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string StoredFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Length { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
