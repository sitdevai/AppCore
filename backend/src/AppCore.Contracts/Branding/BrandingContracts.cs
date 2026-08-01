using System.ComponentModel.DataAnnotations;

namespace AppCore.Contracts.Branding;

public sealed record BrandingResponse(
    string OrganizationName,
    string ShortOrganizationName,
    string PrimaryColor,
    string SecondaryColor,
    string HeaderColor,
    string BackgroundColor,
    string PatternColor,
    string BackgroundPattern,
    string? LightLogoUrl,
    string? DarkLogoUrl,
    string? CompactLogoUrl,
    string? FaviconUrl,
    long Version);

public sealed record UpdateBrandingRequest(
    [Required, StringLength(200)] string OrganizationName,
    [Required, StringLength(80)] string ShortOrganizationName,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string PrimaryColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string SecondaryColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string HeaderColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string BackgroundColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string PatternColor,
    [Required, RegularExpression("^(None|Dots|Grid|Diagonal|Geometric)$")] string BackgroundPattern,
    long ExpectedVersion,
    bool Confirmed);

public sealed record RestoreBrandingRequest(bool Confirmed);
