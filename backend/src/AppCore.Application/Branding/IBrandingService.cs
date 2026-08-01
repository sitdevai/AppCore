namespace AppCore.Application.Branding;

public static class BrandingDefaults
{
    public const string OrganizationName = "AppCore";
    public const string ShortOrganizationName = "Core";
    public const string PrimaryColor = "#1d4ed8";
    public const string SecondaryColor = "#0f766e";
    public const string HeaderColor = "#ffffff";
    public const string BackgroundColor = "#f4f6f8";
    public const string PatternColor = "#1d4ed8";
    public const BrandingBackgroundPattern BackgroundPattern = BrandingBackgroundPattern.None;
}

public enum BrandingBackgroundPattern
{
    None,
    Dots,
    Grid,
    Diagonal,
    Geometric,
}

public enum BrandingAssetKind
{
    LightLogo,
    DarkLogo,
    CompactLogo,
    Favicon,
}

public sealed record BrandingResult(
    string OrganizationName,
    string ShortOrganizationName,
    string PrimaryColor,
    string SecondaryColor,
    string HeaderColor,
    string BackgroundColor,
    string PatternColor,
    BrandingBackgroundPattern BackgroundPattern,
    Guid? LightLogoAssetId,
    Guid? DarkLogoAssetId,
    Guid? CompactLogoAssetId,
    Guid? FaviconAssetId,
    long Version);

public sealed record BrandingAssetUpload(
    string OriginalFileName,
    string ContentType,
    long Length,
    Stream Content);

public sealed record BrandingAssetContent(
    string StoredFileName,
    string ContentType,
    long Length,
    Stream Content);

public interface IBrandingService
{
    Task<BrandingResult> GetPublicAsync(CancellationToken cancellationToken = default);
    Task<BrandingResult> UpdateAsync(Guid actorUserId, string organizationName,
        string shortOrganizationName, string primaryColor, string secondaryColor,
        string headerColor, string backgroundColor,
        string patternColor,
        BrandingBackgroundPattern backgroundPattern,
        long expectedVersion, CancellationToken cancellationToken = default);
    Task<BrandingResult> RestoreDefaultsAsync(Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<BrandingResult> UploadAssetAsync(Guid actorUserId, BrandingAssetKind kind,
        BrandingAssetUpload upload, CancellationToken cancellationToken = default);
    Task<BrandingAssetContent?> OpenAssetAsync(Guid assetId,
        CancellationToken cancellationToken = default);
}

public interface IBrandingFileStore
{
    Task StoreAsync(string storedFileName, Stream content,
        CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storedFileName,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string storedFileName,
        CancellationToken cancellationToken = default);
}
