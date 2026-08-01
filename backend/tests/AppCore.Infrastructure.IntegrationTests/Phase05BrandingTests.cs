using AppCore.Application.Branding;
using AppCore.Application.Common.Exceptions;
using AppCore.Application.Security;
using AppCore.Infrastructure.Branding;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class Phase05BrandingTests(PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task DefaultsAreSeededAndUpdatesAreImmediatelyVisibleAndAudited()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        var service = CreateService(context, new MemoryBrandingFileStore());
        Guid actor = Guid.NewGuid();

        BrandingResult original = await service.GetPublicAsync();
        Assert.Equal(BrandingDefaults.OrganizationName, original.OrganizationName);

        BrandingResult updated = await service.UpdateAsync(
            actor, "Example Organization", "Example", "#112233", "#aabbcc",
            "#223344", "#f1f2f3", "#663399",
            BrandingBackgroundPattern.Geometric, original.Version);
        BrandingResult publicResult = await service.GetPublicAsync();

        Assert.Equal(updated, publicResult);
        Assert.Equal("#aabbcc", publicResult.SecondaryColor);
        Assert.Equal("#223344", publicResult.HeaderColor);
        Assert.Equal("#f1f2f3", publicResult.BackgroundColor);
        Assert.Equal("#663399", publicResult.PatternColor);
        Assert.Equal(BrandingBackgroundPattern.Geometric, publicResult.BackgroundPattern);
        Assert.True(await context.SecurityAuditEvents.AnyAsync(value =>
            value.EventCode == SecurityAuditCodes.VisualIdentityChanged
            && value.ActorUserId == actor));

        await service.RestoreDefaultsAsync(actor);
    }

    [Fact]
    public async Task UploadValidatesNameSignatureAndUpdatesThePublicContract()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        var files = new MemoryBrandingFileStore();
        var service = CreateService(context, files);
        Guid actor = Guid.NewGuid();
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0];

        BrandingResult uploaded = await service.UploadAssetAsync(
            actor,
            BrandingAssetKind.LightLogo,
            new BrandingAssetUpload("logo.png", "image/png", png.Length,
                new MemoryStream(png)));

        Assert.NotNull(uploaded.LightLogoAssetId);
        Assert.Equal(uploaded, await service.GetPublicAsync());
        BrandingAssetContent? content = await service.OpenAssetAsync(
            uploaded.LightLogoAssetId!.Value);
        Assert.NotNull(content);
        Assert.Equal("image/png", content.ContentType);

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            service.UploadAssetAsync(actor, BrandingAssetKind.LightLogo,
                new BrandingAssetUpload("../logo.png", "image/png", png.Length,
                    new MemoryStream(png))));
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            service.UploadAssetAsync(actor, BrandingAssetKind.LightLogo,
                new BrandingAssetUpload("logo.png", "image/png", 4,
                    new MemoryStream([1, 2, 3, 4]))));

        await service.RestoreDefaultsAsync(actor);
    }

    [Fact]
    public async Task UpdateRejectsInvalidColorsAndStaleVersions()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        var service = CreateService(context, new MemoryBrandingFileStore());
        BrandingResult current = await service.GetPublicAsync();

        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.UpdateAsync(
            Guid.NewGuid(), "Name", "Short", "red", "#112233", "#ffffff", "#f4f6f8", "#1d4ed8",
            BrandingBackgroundPattern.None, current.Version));
        await Assert.ThrowsAsync<ApplicationConflictException>(() => service.UpdateAsync(
            Guid.NewGuid(), "Name", "Short", "#112233", "#445566", "#ffffff", "#f4f6f8", "#1d4ed8",
            BrandingBackgroundPattern.None, current.Version - 1));
    }

    private static BrandingService CreateService(ApplicationDbContext context,
        IBrandingFileStore files) => new(
            context,
            files,
            new SecurityAuditWriter(context, TimeProvider.System),
            TimeProvider.System);

    private async Task<ApplicationDbContext> CreateMigratedContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseNpgsql(database.ConnectionString, npgsql => npgsql
            .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
            .MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Infrastructure));
        var context = new ApplicationDbContext(options.Options);
        await context.Database.MigrateAsync();
        return context;
    }

    private sealed class MemoryBrandingFileStore : IBrandingFileStore
    {
        private readonly Dictionary<string, byte[]> values = [];

        public async Task StoreAsync(string storedFileName, Stream content,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            values.Add(storedFileName, buffer.ToArray());
        }

        public Task<Stream?> OpenReadAsync(string storedFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(values.TryGetValue(storedFileName, out byte[]? value)
                ? new MemoryStream(value, writable: false)
                : null);

        public Task DeleteAsync(string storedFileName,
            CancellationToken cancellationToken = default)
        {
            values.Remove(storedFileName);
            return Task.CompletedTask;
        }
    }
}
