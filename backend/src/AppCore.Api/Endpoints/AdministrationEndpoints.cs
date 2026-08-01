using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
using AppCore.Api.Security;
using AppCore.Api.Validation;
using AppCore.Application.Security;
using AppCore.Contracts.Administration;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppCore.Api.Endpoints;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ApiVersionSet versions = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v{version:apiVersion}/administration")
            .WithApiVersionSet(versions)
            .MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Administration")
            .AddEndpointFilter<DataAnnotationsValidationFilter>();

        group.MapGet("/users", ListUsersAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.UsersView));
        group.MapGet("/users/{userId:guid}", GetUserAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.UsersView));
        group.MapPost("/users", CreateUserAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.UsersCreate));
        group.MapPut("/users/{userId:guid}", UpdateUserAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.UsersUpdate));
        group.MapPost("/users/{userId:guid}/transitions/{operation}", TransitionUserAsync);
        group.MapPost("/users/{userId:guid}/challenges/{purpose}", IssueChallengeAsync);
        group.MapPost("/users/{userId:guid}/mfa-recovery", StartMfaRecoveryAsync)
            .RequireAuthorization(
                PermissionPolicies.For(SystemPermissions.UsersResetMfa),
                PermissionPolicies.For(SystemPermissions.UsersIssueMfaRecovery));

        group.MapGet("/roles", ListRolesAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesView));
        group.MapPost("/roles", CreateRoleAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesCreate));
        group.MapPut("/roles/{roleId:guid}", RenameRoleAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesUpdate));
        group.MapPost("/roles/{roleId:guid}/archive", ArchiveRoleAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesArchive));
        group.MapPut("/roles/{roleId:guid}/permissions", ReplacePermissionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.PermissionsAssignToRoles));
        group.MapPost("/users/{userId:guid}/roles", AssignRoleAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesAssignToUsers));
        group.MapDelete("/users/{userId:guid}/roles/{roleId:guid}", RemoveRoleAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.RolesAssignToUsers));
        group.MapGet("/permissions", ListPermissionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.PermissionsView));
        return endpoints;
    }

    private static async Task<IResult> ListUsersAsync(
        string? search,
        IAdministrationService administration,
        CancellationToken cancellationToken) =>
        TypedResults.Ok((await administration.ListUsersAsync(search, cancellationToken))
            .Select(MapUser));

    private static async Task<IResult> GetUserAsync(
        Guid userId,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        AdministrationUserResult? user = await administration.GetUserAsync(userId, cancellationToken);
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(MapUser(user));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateAdministrationUserRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor))
        {
            return TypedResults.BadRequest();
        }
        await antiforgery.ValidateRequestAsync(context);
        AccountCreationResult created = await administration.CreateUserAsync(
            actor, request.Username, request.Email, cancellationToken);
        return TypedResults.Created($"/api/v1/administration/users/{created.UserId}", created);
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UpdateAdministrationUserRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await administration.UpdateEmailAsync(
            actor, userId, request.Email, request.ExpectedAuthorizationVersion, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Conflict();
    }

    private static async Task<IResult> TransitionUserAsync(
        Guid userId,
        string operation,
        UserTransitionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        string? permission = operation switch
        {
            "enable" => SystemPermissions.UsersEnable,
            "disable" => SystemPermissions.UsersDisable,
            "suspend" => SystemPermissions.UsersSuspend,
            "archive" => SystemPermissions.UsersArchive,
            "restore" => SystemPermissions.UsersRestore,
            _ => null,
        };
        if (permission is null || !request.Confirmed || !TryActor(context.User, out Guid actor))
            return TypedResults.BadRequest();
        IAuthorizationService authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
        if (!(await authorization.AuthorizeAsync(context.User, PermissionPolicies.For(permission))).Succeeded)
            return TypedResults.Forbid();
        await antiforgery.ValidateRequestAsync(context);
        return await administration.TransitionUserAsync(
            actor, userId, operation, request.ExpectedAuthorizationVersion, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Conflict();
    }

    private static async Task<IResult> IssueChallengeAsync(
        Guid userId,
        string purpose,
        UserTransitionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        string? permission = purpose switch
        {
            "activation" => SystemPermissions.UsersIssueActivation,
            "password-reset" => SystemPermissions.UsersResetPassword,
            _ => null,
        };
        if (permission is null || !request.Confirmed || !TryActor(context.User, out Guid actor))
            return TypedResults.BadRequest();
        IAuthorizationService authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
        if (!(await authorization.AuthorizeAsync(context.User, PermissionPolicies.For(permission))).Succeeded)
            return TypedResults.Forbid();
        await antiforgery.ValidateRequestAsync(context);
        OneTimeChallengeResult? challenge = await administration.IssueChallengeAsync(
            actor, userId, purpose, request.ExpectedAuthorizationVersion, cancellationToken);
        if (challenge is null) return TypedResults.Conflict();
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new OneTimeAdministrationChallengeResponse(
            challenge.UserId, challenge.Code, challenge.ExpiresAtUtc));
    }

    private static async Task<IResult> StartMfaRecoveryAsync(
        Guid userId,
        UserTransitionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAdministrationService administration,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        OneTimeChallengeResult? challenge = await administration.StartMfaRecoveryAsync(
            actor, userId, request.ExpectedAuthorizationVersion, cancellationToken);
        if (challenge is null) return TypedResults.Conflict();
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new OneTimeAdministrationChallengeResponse(
            challenge.UserId, challenge.Code, challenge.ExpiresAtUtc));
    }

    private static async Task<IResult> ListRolesAsync(
        IAdministrationService administration,
        CancellationToken cancellationToken) =>
        TypedResults.Ok((await administration.ListRolesAsync(cancellationToken)).Select(MapRole));

    private static async Task<IResult> ListPermissionsAsync(
        IAdministrationService administration,
        CancellationToken cancellationToken) =>
        TypedResults.Ok((await administration.ListPermissionsAsync(cancellationToken)).Select(value =>
            new AdministrationPermissionResponse(value.PermissionId, value.Assurance, value.Scope)));

    private static async Task<IResult> CreateRoleAsync(CreateRoleRequest request, HttpContext context,
        IAntiforgery antiforgery, IRoleAuthorizationService roles, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        Guid? roleId = await roles.CreateRoleAsync(actor, request.Name, cancellationToken);
        return roleId.HasValue ? TypedResults.Created($"/api/v1/administration/roles/{roleId}") : TypedResults.Conflict();
    }

    private static async Task<IResult> RenameRoleAsync(Guid roleId, UpdateRoleRequest request, HttpContext context,
        IAntiforgery antiforgery, IRoleAuthorizationService roles, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await roles.RenameRoleAsync(actor, roleId, request.Name, request.ExpectedConcurrencyStamp, cancellationToken)
            ? TypedResults.NoContent() : TypedResults.Conflict();
    }

    private static async Task<IResult> ArchiveRoleAsync(Guid roleId, UpdateRoleRequest request, HttpContext context,
        IAntiforgery antiforgery, IRoleAuthorizationService roles, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await roles.ArchiveRoleAsync(actor, roleId, request.ExpectedConcurrencyStamp, cancellationToken)
            ? TypedResults.NoContent() : TypedResults.Conflict();
    }

    private static async Task<IResult> ReplacePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request,
        HttpContext context, IAntiforgery antiforgery, IRoleAuthorizationService roles,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await roles.ReplaceRolePermissionsAsync(
            actor, roleId, request.PermissionIds, request.ExpectedConcurrencyStamp, cancellationToken)
            ? TypedResults.NoContent() : TypedResults.Conflict();
    }

    private static async Task<IResult> AssignRoleAsync(Guid userId, RoleAssignmentRequest request,
        HttpContext context, IAntiforgery antiforgery, IRoleAuthorizationService roles,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await roles.AssignRoleAsync(actor, userId, request.RoleId,
            request.ExpectedRoleConcurrencyStamp, cancellationToken)
            ? TypedResults.NoContent() : TypedResults.Conflict();
    }

    private static async Task<IResult> RemoveRoleAsync(Guid userId, Guid roleId,
        [FromBody] RemoveRoleAssignmentRequest request, HttpContext context, IAntiforgery antiforgery,
        IRoleAuthorizationService roles, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context.User, out Guid actor)) return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        return await roles.RemoveRoleAsync(actor, userId, roleId, cancellationToken)
            ? TypedResults.NoContent() : TypedResults.Conflict();
    }

    private static AdministrationUserResponse MapUser(AdministrationUserResult value) =>
        new(value.UserId, value.Username, value.Email, value.AccountStatus,
            value.CredentialStatus, value.MfaState, value.AuthorizationVersion,
            value.IsProtectedOwner, value.RoleIds);

    private static AdministrationRoleResponse MapRole(AdministrationRoleResult value) =>
        new(value.RoleId, value.Name, value.IsBuiltIn, value.IsProtected, value.IsArchived,
            value.ConcurrencyStamp, value.PermissionIds);

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorUserId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId);
}
