using System.Security.Claims;
using AppCore.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace AppCore.Api.Security;

public sealed partial class AuthenticatedSessionMiddleware(
    RequestDelegate next,
    ILogger<AuthenticatedSessionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISessionValidator sessionValidator)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        Endpoint? endpoint = context.GetEndpoint();
        bool allowsAnonymous =
            endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        bool recoveryOnly = endpoint?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(data =>
                string.Equals(
                    data.Policy,
                    "RecoveryOnly",
                    StringComparison.Ordinal)
                || string.Equals(
                    data.AuthenticationSchemes,
                    AuthenticationSchemes.Recovery,
                    StringComparison.Ordinal)) == true;

        if (recoveryOnly)
        {
            await ClearStaleSessionAsync(context);
            await next(context);
            return;
        }

        string? sessionClaim =
            context.User.FindFirstValue(AuthenticationSchemes.SessionIdClaim);
        string? versionClaim =
            context.User.FindFirstValue(AuthenticationSchemes.AuthorizationVersionClaim);
        string? userClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sessionClaim, out Guid sessionId)
            || !long.TryParse(versionClaim, out long authorizationVersion)
            || !Guid.TryParse(userClaim, out Guid principalUserId))
        {
            await HandleInvalidSessionAsync(context, allowsAnonymous, next);
            return;
        }

        ValidatedSession? session;
        try
        {
            session = await sessionValidator.ValidateAsync(
                sessionId,
                authorizationVersion,
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogSessionValidationFailure(
                logger,
                context.TraceIdentifier,
                exception.GetType().Name);
            await RejectAsync(context, StatusCodes.Status503ServiceUnavailable);
            return;
        }

        if (session is null || session.UserId != principalUserId)
        {
            await HandleInvalidSessionAsync(context, allowsAnonymous, next);
            return;
        }

        context.Items[nameof(ValidatedSession)] = session;
        await next(context);
    }

    private static async Task HandleInvalidSessionAsync(
        HttpContext context,
        bool allowsAnonymous,
        RequestDelegate next)
    {
        await ClearStaleSessionAsync(context);
        if (allowsAnonymous)
        {
            await next(context);
            return;
        }

        await RejectAsync(context, StatusCodes.Status401Unauthorized);
    }

    private static async Task ClearStaleSessionAsync(HttpContext context)
    {
        context.Response.Cookies.Delete(
            AuthenticationSchemes.SessionCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        await Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Authoritative session validation failed closed. TraceId: {TraceId}, ExceptionType: {ExceptionType}")]
    private static partial void LogSessionValidationFailure(
        ILogger logger,
        string traceId,
        string exceptionType);

    private static async Task RejectAsync(HttpContext context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        IProblemDetailsService problemDetails =
            context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = statusCode,
                    Title = statusCode == StatusCodes.Status401Unauthorized
                        ? "Authentication is required."
                        : "Session validation is temporarily unavailable.",
                },
            });
    }
}
