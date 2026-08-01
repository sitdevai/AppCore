using Asp.Versioning;
using Asp.Versioning.Builder;
using AppCore.Api.Validation;
using AppCore.Contracts.System;

namespace AppCore.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ApiVersionSet versionSet = endpoints
            .NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder api = endpoints
            .MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(versionSet)
            .AddEndpointFilter<DataAnnotationsValidationFilter>();

        api.MapGet(
                "/system/info",
                (TimeProvider timeProvider) =>
                    TypedResults.Ok(
                        new SystemInfoResponse(
                            "AppCore.Api",
                            "1.0",
                            timeProvider.GetUtcNow())))
            .MapToApiVersion(new ApiVersion(1, 0))
            .WithName("GetSystemInfoV1")
            .WithTags("System")
            .AllowAnonymous();

        return endpoints;
    }
}
