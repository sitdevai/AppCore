using System.Globalization;
using System.Security.Claims;
using System.Text;
using AppCore.Api.Security;
using AppCore.Application.Security;
using AppCore.Contracts.Administration;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace AppCore.Api.Endpoints;

public static class SecurityAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapSecurityAdministrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ApiVersionSet versions = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0)).ReportApiVersions().Build();
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(versions).MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Security Administration");

        group.MapGet("/sessions/me", ListOwnSessionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsViewOwn));
        group.MapDelete("/sessions/me/{sessionId:guid}", RevokeOwnSessionAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsRevokeOwn));
        group.MapPost("/sessions/me/revoke-all", RevokeOwnSessionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsRevokeOwn));
        group.MapGet("/sessions/users/{userId:guid}", ListUserSessionsAsync)
            .RequireAuthorization(
                PermissionPolicies.For(SystemPermissions.SessionsViewForUser),
                PermissionPolicies.For(SystemPermissions.UsersView));
        group.MapDelete("/sessions/users/{userId:guid}/{sessionId:guid}", RevokeUserSessionAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsRevokeForUser));
        group.MapPost("/sessions/users/{userId:guid}/revoke-all", RevokeUserSessionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsRevokeForUser));
        group.MapPost("/sessions/revoke-global", RevokeGlobalSessionsAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.SessionsRevokeGlobal));

        group.MapGet("/security-audit", SearchAuditAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.AuditSecurityView));
        group.MapPost("/security-audit/export", ExportAuditAsync)
            .RequireAuthorization(PermissionPolicies.For(SystemPermissions.AuditSecurityExport));
        return endpoints;
    }

    private static async Task<IResult> ListOwnSessionsAsync(
        HttpContext context, ISecurityAdministrationService service,
        CancellationToken cancellationToken) =>
        TryActor(context, out Guid actor, out Guid session)
            ? TypedResults.Ok(Map(await service.ListSessionsAsync(
                actor, session, null, cancellationToken)))
            : TypedResults.Unauthorized();

    private static async Task<IResult> ListUserSessionsAsync(
        Guid userId, HttpContext context, ISecurityAdministrationService service,
        CancellationToken cancellationToken) =>
        TryActor(context, out Guid actor, out Guid session)
            ? TypedResults.Ok(Map(await service.ListSessionsAsync(
                actor, session, userId, cancellationToken)))
            : TypedResults.Unauthorized();

    private static Task<IResult> RevokeOwnSessionAsync(
        Guid sessionId, [FromBody] ConfirmedSecurityActionRequest request, HttpContext context,
        IAntiforgery antiforgery, ISecurityAdministrationService service,
        CancellationToken cancellationToken) =>
        RevokeSessionAsync(null, sessionId, request, context, antiforgery, service, cancellationToken);

    private static Task<IResult> RevokeUserSessionAsync(
        Guid userId, Guid sessionId, [FromBody] ConfirmedSecurityActionRequest request,
        HttpContext context, IAntiforgery antiforgery,
        ISecurityAdministrationService service, CancellationToken cancellationToken) =>
        RevokeSessionAsync(userId, sessionId, request, context, antiforgery, service, cancellationToken);

    private static async Task<IResult> RevokeSessionAsync(
        Guid? targetUserId, Guid sessionId, ConfirmedSecurityActionRequest request,
        HttpContext context, IAntiforgery antiforgery,
        ISecurityAdministrationService service, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context, out Guid actor, out Guid currentSession))
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        bool revoked = await service.RevokeSessionAsync(
            actor, currentSession, targetUserId ?? actor, sessionId, cancellationToken);
        return revoked ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<IResult> RevokeOwnSessionsAsync(
        ConfirmedSecurityActionRequest request, HttpContext context,
        IAntiforgery antiforgery, ISecurityAdministrationService service,
        CancellationToken cancellationToken) =>
        RevokeSessionsAsync(null, request, context, antiforgery, service, cancellationToken);

    private static Task<IResult> RevokeUserSessionsAsync(
        Guid userId, ConfirmedSecurityActionRequest request, HttpContext context,
        IAntiforgery antiforgery, ISecurityAdministrationService service,
        CancellationToken cancellationToken) =>
        RevokeSessionsAsync(userId, request, context, antiforgery, service, cancellationToken);

    private static async Task<IResult> RevokeSessionsAsync(
        Guid? targetUserId, ConfirmedSecurityActionRequest request, HttpContext context,
        IAntiforgery antiforgery, ISecurityAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context, out Guid actor, out Guid currentSession))
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        int count = await service.RevokeUserSessionsAsync(
            actor, currentSession, targetUserId ?? actor, cancellationToken);
        return TypedResults.Ok(new { revokedCount = count });
    }

    private static async Task<IResult> RevokeGlobalSessionsAsync(
        ConfirmedSecurityActionRequest request, HttpContext context,
        IAntiforgery antiforgery, ISecurityAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context, out Guid actor, out _))
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        int count = await service.RevokeGlobalSessionsAsync(actor, cancellationToken);
        return TypedResults.Ok(new { revokedCount = count });
    }

    private static async Task<IResult> SearchAuditAsync(
        string? eventCode, Guid? actorUserId, Guid? targetUserId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int page, int pageSize,
        string? sortBy, string? sortDirection,
        HttpContext context, ISecurityAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out Guid actor, out _) || fromUtc > toUtc)
            return TypedResults.BadRequest();
        SecurityAuditPage result = await service.SearchAuditAsync(
            actor, new SecurityAuditQuery(eventCode, actorUserId, targetUserId,
                fromUtc, toUtc, page, pageSize, sortBy ?? "occurredAtUtc",
                sortDirection ?? "desc"), cancellationToken);
        return TypedResults.Ok(new SecurityAuditPageResponse(
            result.Items.Select(MapAudit).ToArray(), result.Page,
            result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> ExportAuditAsync(
        ConfirmedSecurityActionRequest request, string? eventCode,
        Guid? actorUserId, Guid? targetUserId, DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc, string? sortBy, string? sortDirection,
        HttpContext context, IAntiforgery antiforgery,
        ISecurityAdministrationService service, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || !TryActor(context, out Guid actor, out _)
            || fromUtc > toUtc)
            return TypedResults.BadRequest();
        await antiforgery.ValidateRequestAsync(context);
        IReadOnlyList<SecurityAuditResult> items = await service.ExportAuditAsync(
            actor, new SecurityAuditQuery(eventCode, actorUserId, targetUserId,
                fromUtc, toUtc, 1, 10_000, sortBy ?? "occurredAtUtc",
                sortDirection ?? "desc"), cancellationToken);
        var csv = new StringBuilder("id,eventCode,resultCode,occurredAtUtc,actorUserId,targetUserId,correlationId\r\n");
        foreach (SecurityAuditResult item in items)
        {
            csv.Append(item.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(item.EventCode)).Append(',').Append(Csv(item.ResultCode)).Append(',')
                .Append(item.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.ActorUserId).Append(',').Append(item.TargetUserId).Append(',')
                .Append(Csv(item.CorrelationId)).Append("\r\n");
        }
        return Results.File(Encoding.UTF8.GetPreamble().Concat(
            Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8",
            $"security-audit-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Csv(string value)
    {
        string safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private static IEnumerable<SessionAdministrationResponse> Map(
        IReadOnlyList<SessionAdministrationResult> values) => values.Select(value =>
            new SessionAdministrationResponse(value.SessionId, value.UserId,
                value.CreatedAtUtc, value.LastActivityAtUtc, value.AbsoluteExpiresAtUtc,
                value.MfaVerifiedAtUtc, value.AuthenticationMethods, value.DeviceLabel,
                value.ClientCategory, value.IsCurrent));

    private static SecurityAuditResponse MapAudit(SecurityAuditResult value) =>
        new(value.Id, value.EventCode, value.ResultCode, value.OccurredAtUtc,
            value.ActorUserId, value.TargetUserId, value.CorrelationId,
            value.DetailsJson, value.SourceIp, value.UserAgent);

    private static bool TryActor(HttpContext context, out Guid actor, out Guid session)
    {
        session = default;
        return Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor)
        && Guid.TryParse(context.User.FindFirstValue(AuthenticationSchemes.SessionIdClaim), out session);
    }
}
