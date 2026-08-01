using System.Security.Claims;
using AppCore.Api.Security;
using AppCore.Api.Validation;
using AppCore.Application.Branding;
using AppCore.Application.Security;
using AppCore.Contracts.Branding;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Antiforgery;

namespace AppCore.Api.Endpoints;

public static class BrandingEndpoints
{
    public static IEndpointRouteBuilder MapBrandingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ApiVersionSet versions = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0)).ReportApiVersions().Build();
        RouteGroupBuilder publicGroup = endpoints.MapGroup("/api/v{version:apiVersion}/branding")
            .WithApiVersionSet(versions).MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Branding");
        publicGroup.MapGet("/", GetAsync).AllowAnonymous();
        publicGroup.MapGet("/assets/{assetId:guid}", GetAssetAsync).AllowAnonymous();

        RouteGroupBuilder settings = endpoints
            .MapGroup("/api/v{version:apiVersion}/settings/visual-identity")
            .WithApiVersionSet(versions).MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Visual Identity")
            .AddEndpointFilter<DataAnnotationsValidationFilter>();
        settings.MapGet("/", GetAsync).RequireAuthorization(
            PermissionPolicies.For(SystemPermissions.SettingsVisualIdentityView));
        settings.MapPut("/", UpdateAsync).RequireAuthorization(
            PermissionPolicies.For(SystemPermissions.SettingsVisualIdentityUpdate));
        settings.MapPost("/restore-defaults", RestoreAsync).RequireAuthorization(
            PermissionPolicies.For(SystemPermissions.SettingsVisualIdentityUpdate));
        settings.MapPost("/assets/{kind}", UploadAsync).RequireAuthorization(
            PermissionPolicies.For(SystemPermissions.SettingsVisualIdentityUpdate));
        return endpoints;
    }

    private static async Task<IResult> GetAsync(IBrandingService branding,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(Map(await branding.GetPublicAsync(cancellationToken)));

    private static async Task<IResult> UpdateAsync(UpdateBrandingRequest request,
        HttpContext context, IAntiforgery antiforgery, IBrandingService branding,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor))
            return TypedResults.BadRequest();
        if (!Enum.TryParse(request.BackgroundPattern, out BrandingBackgroundPattern pattern))
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return TypedResults.Ok(Map(await branding.UpdateAsync(actor,
            request.OrganizationName, request.ShortOrganizationName,
            request.PrimaryColor, request.SecondaryColor,
            request.HeaderColor, request.BackgroundColor, request.PatternColor, pattern,
            request.ExpectedVersion, cancellationToken)));
    }

    private static async Task<IResult> RestoreAsync(RestoreBrandingRequest request,
        HttpContext context, IAntiforgery antiforgery, IBrandingService branding,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor))
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return TypedResults.Ok(Map(await branding.RestoreDefaultsAsync(actor, cancellationToken)));
    }

    private static async Task<IResult> UploadAsync(string kind, HttpContext context,
        IAntiforgery antiforgery, IBrandingService branding,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context.User, out Guid actor)
            || !Enum.TryParse(kind, true, out BrandingAssetKind parsedKind)
            || !context.Request.HasFormContentType)
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
        IFormFile? file = form.Files.GetFile("file");
        if (file is null || form["confirmed"] != "true") return TypedResults.BadRequest();
        await using Stream content = file.OpenReadStream();
        BrandingResult result = await branding.UploadAssetAsync(actor, parsedKind,
            new BrandingAssetUpload(file.FileName, file.ContentType, file.Length, content),
            cancellationToken);
        return TypedResults.Ok(Map(result));
    }

    private static async Task<IResult> GetAssetAsync(Guid assetId,
        IBrandingService branding, HttpContext context, CancellationToken cancellationToken)
    {
        BrandingAssetContent? asset = await branding.OpenAssetAsync(assetId, cancellationToken);
        if (asset is null) return TypedResults.NotFound();
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return Results.Stream(asset.Content, asset.ContentType, enableRangeProcessing: true);
    }

    private static BrandingResponse Map(BrandingResult value)
    {
        static string? Url(Guid? id) => id.HasValue ? $"/api/v1/branding/assets/{id}" : null;
        return new BrandingResponse(value.OrganizationName, value.ShortOrganizationName,
            value.PrimaryColor, value.SecondaryColor, value.HeaderColor,
            value.BackgroundColor, value.PatternColor, value.BackgroundPattern.ToString(),
            Url(value.LightLogoAssetId),
            Url(value.DarkLogoAssetId), Url(value.CompactLogoAssetId),
            Url(value.FaviconAssetId), value.Version);
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid actor) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}
