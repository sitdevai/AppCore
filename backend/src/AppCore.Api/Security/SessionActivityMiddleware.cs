using AppCore.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace AppCore.Api.Security;

public sealed partial class SessionActivityMiddleware(
    RequestDelegate next,
    ILogger<SessionActivityMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISessionValidator sessionValidator)
    {
        Endpoint? endpoint = context.GetEndpoint();
        bool isProtected = endpoint is not null
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && context.User.Identity?.IsAuthenticated == true;

        if (isProtected
            && context.Items[nameof(ValidatedSession)] is ValidatedSession session)
        {
            bool touched;
            try
            {
                touched = await sessionValidator.TouchAsync(
                    session.SessionId,
                    session.AuthorizationVersion,
                    context.RequestAborted);
            }
            catch (OperationCanceledException)
                when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogSessionTouchFailure(
                    logger,
                    context.TraceIdentifier,
                    exception.GetType().Name);
                context.Response.StatusCode =
                    StatusCodes.Status503ServiceUnavailable;
                IProblemDetailsService problemDetails =
                    context.RequestServices
                        .GetRequiredService<IProblemDetailsService>();
                await problemDetails.TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails =
                            new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Status = StatusCodes.Status503ServiceUnavailable,
                                Title =
                                    "Session validation is temporarily unavailable.",
                            },
                    });
                return;
            }
            if (!touched)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next(context);
    }

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Warning,
        Message = "Session activity update failed closed. TraceId: {TraceId}, ExceptionType: {ExceptionType}")]
    private static partial void LogSessionTouchFailure(
        ILogger logger,
        string traceId,
        string exceptionType);
}
