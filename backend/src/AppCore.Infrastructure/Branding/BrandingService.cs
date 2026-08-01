using System.Text.RegularExpressions;
using AppCore.Application.Branding;
using AppCore.Application.Common.Exceptions;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Branding;

public sealed partial class BrandingService(
    ApplicationDbContext context,
    IBrandingFileStore files,
    ISecurityAuditWriter auditWriter,
    TimeProvider timeProvider) : IBrandingService
{
    private const long MaximumFileLength = 2 * 1024 * 1024;

    public async Task<BrandingResult> GetPublicAsync(
        CancellationToken cancellationToken = default)
    {
        VisualIdentitySettings? value = await context.VisualIdentitySettings
            .AsNoTracking().SingleOrDefaultAsync(value => value.Id == 1, cancellationToken);
        return value is null ? Defaults() : Map(value);
    }

    public async Task<BrandingResult> UpdateAsync(Guid actorUserId,
        string organizationName, string shortOrganizationName,
        string primaryColor, string secondaryColor, string headerColor,
        string backgroundColor, string patternColor,
        BrandingBackgroundPattern backgroundPattern,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateText(organizationName, 200, nameof(organizationName));
        ValidateText(shortOrganizationName, 80, nameof(shortOrganizationName));
        ValidateColor(primaryColor, nameof(primaryColor));
        ValidateColor(secondaryColor, nameof(secondaryColor));
        ValidateColor(headerColor, nameof(headerColor));
        ValidateColor(backgroundColor, nameof(backgroundColor));
        ValidateColor(patternColor, nameof(patternColor));
        VisualIdentitySettings settings = await GetSettingsAsync(cancellationToken);
        if (settings.Version != expectedVersion)
            throw new ApplicationConflictException("Visual identity version conflict.");
        settings.OrganizationName = organizationName.Trim();
        settings.ShortOrganizationName = shortOrganizationName.Trim();
        settings.PrimaryColor = primaryColor.ToLowerInvariant();
        settings.SecondaryColor = secondaryColor.ToLowerInvariant();
        settings.HeaderColor = headerColor.ToLowerInvariant();
        settings.BackgroundColor = backgroundColor.ToLowerInvariant();
        settings.PatternColor = patternColor.ToLowerInvariant();
        settings.BackgroundPattern = backgroundPattern;
        settings.Version++;
        settings.UpdatedAtUtc = timeProvider.GetUtcNow();
        await SaveAndAuditAsync(actorUserId, cancellationToken);
        return Map(settings);
    }

    public async Task<BrandingResult> RestoreDefaultsAsync(Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        VisualIdentitySettings settings = await GetSettingsAsync(cancellationToken);
        Guid?[] ids = [settings.LightLogoAssetId, settings.DarkLogoAssetId,
            settings.CompactLogoAssetId, settings.FaviconAssetId];
        settings.OrganizationName = BrandingDefaults.OrganizationName;
        settings.ShortOrganizationName = BrandingDefaults.ShortOrganizationName;
        settings.PrimaryColor = BrandingDefaults.PrimaryColor;
        settings.SecondaryColor = BrandingDefaults.SecondaryColor;
        settings.HeaderColor = BrandingDefaults.HeaderColor;
        settings.BackgroundColor = BrandingDefaults.BackgroundColor;
        settings.PatternColor = BrandingDefaults.PatternColor;
        settings.BackgroundPattern = BrandingDefaults.BackgroundPattern;
        settings.LightLogoAssetId = settings.DarkLogoAssetId =
            settings.CompactLogoAssetId = settings.FaviconAssetId = null;
        settings.Version++;
        settings.UpdatedAtUtc = timeProvider.GetUtcNow();
        BrandingAsset[] assets = await context.BrandingAssets
            .Where(value => ids.Contains(value.Id)).ToArrayAsync(cancellationToken);
        context.BrandingAssets.RemoveRange(assets);
        await SaveAndAuditAsync(actorUserId, cancellationToken);
        foreach (BrandingAsset asset in assets)
            await files.DeleteAsync(asset.StoredFileName, cancellationToken);
        return Map(settings);
    }

    public async Task<BrandingResult> UploadAssetAsync(Guid actorUserId,
        BrandingAssetKind kind, BrandingAssetUpload upload,
        CancellationToken cancellationToken = default)
    {
        (string extension, string contentType) = ValidateUpload(kind, upload);
        string storedName = $"{Guid.NewGuid():N}{extension}";
        await files.StoreAsync(storedName, upload.Content, cancellationToken);
        try
        {
            VisualIdentitySettings settings = await GetSettingsAsync(cancellationToken);
            Guid? oldId = GetAssetId(settings, kind);
            var asset = new BrandingAsset
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                OriginalFileName = upload.OriginalFileName,
                StoredFileName = storedName,
                ContentType = contentType,
                Length = upload.Length,
                CreatedAtUtc = timeProvider.GetUtcNow(),
            };
            context.BrandingAssets.Add(asset);
            SetAssetId(settings, kind, asset.Id);
            settings.Version++;
            settings.UpdatedAtUtc = timeProvider.GetUtcNow();
            BrandingAsset? old = oldId.HasValue
                ? await context.BrandingAssets.SingleOrDefaultAsync(
                    value => value.Id == oldId, cancellationToken) : null;
            if (old is not null) context.BrandingAssets.Remove(old);
            await SaveAndAuditAsync(actorUserId, cancellationToken);
            if (old is not null) await files.DeleteAsync(old.StoredFileName, cancellationToken);
            return Map(settings);
        }
        catch
        {
            await files.DeleteAsync(storedName, cancellationToken);
            throw;
        }
    }

    public async Task<BrandingAssetContent?> OpenAssetAsync(Guid assetId,
        CancellationToken cancellationToken = default)
    {
        BrandingAsset? asset = await context.BrandingAssets.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == assetId, cancellationToken);
        if (asset is null) return null;
        Stream? content = await files.OpenReadAsync(asset.StoredFileName, cancellationToken);
        return content is null ? null : new BrandingAssetContent(
            asset.StoredFileName, asset.ContentType, asset.Length, content);
    }

    private async Task<VisualIdentitySettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await context.VisualIdentitySettings.SingleAsync(value => value.Id == 1, cancellationToken);

    private async Task SaveAndAuditAsync(Guid actor, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(new SecurityAuditEntry(
            SecurityAuditCodes.VisualIdentityChanged, "success", actor), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static (string Extension, string ContentType) ValidateUpload(
        BrandingAssetKind kind, BrandingAssetUpload upload)
    {
        if (upload.Length is <= 0 or > MaximumFileLength
            || upload.OriginalFileName != Path.GetFileName(upload.OriginalFileName)
            || upload.OriginalFileName.Length > 255
            || !SafeFileName().IsMatch(upload.OriginalFileName))
            throw Validation("File", "validation.invalid");
        string extension = Path.GetExtension(upload.OriginalFileName).ToLowerInvariant();
        byte[] header = new byte[12];
        int read = upload.Content.Read(header, 0, header.Length);
        if (upload.Content.CanSeek) upload.Content.Position = 0;
        bool png = read >= 8 && header.AsSpan()[..8].SequenceEqual(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        bool jpeg = read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        bool webp = read >= 12 && header.AsSpan()[..4].SequenceEqual("RIFF"u8.ToArray())
            && header.AsSpan()[8..12].SequenceEqual("WEBP"u8.ToArray());
        bool ico = read >= 4 && header.AsSpan()[..4].SequenceEqual(new byte[] { 0, 0, 1, 0 });
        return (kind, extension, upload.ContentType.ToLowerInvariant()) switch
        {
            (BrandingAssetKind.Favicon, ".ico", "image/x-icon") when ico => (extension, "image/x-icon"),
            (_, ".png", "image/png") when png => (extension, "image/png"),
            (not BrandingAssetKind.Favicon, ".jpg" or ".jpeg", "image/jpeg") when jpeg => (extension, "image/jpeg"),
            (not BrandingAssetKind.Favicon, ".webp", "image/webp") when webp => (extension, "image/webp"),
            _ => throw Validation("File", "validation.invalid"),
        };
    }

    private static void ValidateText(string value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum
            || value.Any(char.IsControl)) throw Validation(name, "validation.invalid");
    }

    private static void ValidateColor(string value, string name)
    {
        if (!Color().IsMatch(value)) throw Validation(name, "validation.invalid");
    }

    private static Guid? GetAssetId(VisualIdentitySettings value, BrandingAssetKind kind) => kind switch
    {
        BrandingAssetKind.LightLogo => value.LightLogoAssetId,
        BrandingAssetKind.DarkLogo => value.DarkLogoAssetId,
        BrandingAssetKind.CompactLogo => value.CompactLogoAssetId,
        BrandingAssetKind.Favicon => value.FaviconAssetId,
        _ => null,
    };

    private static void SetAssetId(VisualIdentitySettings value, BrandingAssetKind kind, Guid id)
    {
        if (kind == BrandingAssetKind.LightLogo) value.LightLogoAssetId = id;
        else if (kind == BrandingAssetKind.DarkLogo) value.DarkLogoAssetId = id;
        else if (kind == BrandingAssetKind.CompactLogo) value.CompactLogoAssetId = id;
        else if (kind == BrandingAssetKind.Favicon) value.FaviconAssetId = id;
    }

    private static BrandingResult Map(VisualIdentitySettings value) => new(
        value.OrganizationName, value.ShortOrganizationName, value.PrimaryColor,
        value.SecondaryColor, value.HeaderColor, value.BackgroundColor,
        value.PatternColor, value.BackgroundPattern, value.LightLogoAssetId, value.DarkLogoAssetId,
        value.CompactLogoAssetId, value.FaviconAssetId, value.Version);

    private static BrandingResult Defaults() => new(
        BrandingDefaults.OrganizationName, BrandingDefaults.ShortOrganizationName,
        BrandingDefaults.PrimaryColor, BrandingDefaults.SecondaryColor,
        BrandingDefaults.HeaderColor, BrandingDefaults.BackgroundColor,
        BrandingDefaults.PatternColor, BrandingDefaults.BackgroundPattern,
        null, null, null, null, 1);

    private static ApplicationValidationException Validation(string field, string code) =>
        new(new Dictionary<string, string[]> { [field] = [code] });

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex Color();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,254}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFileName();
}
