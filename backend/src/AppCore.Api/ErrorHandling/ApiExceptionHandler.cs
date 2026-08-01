using AppCore.Application.Common.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppCore.Api.ErrorHandling;

public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException
            && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogClientAbortedRequest(logger);
            return true;
        }

        ProblemDetails problemDetails = CreateProblemDetails(exception);

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(
                logger,
                exception.GetType().FullName ?? exception.GetType().Name);
        }
        else
        {
            LogHandledException(
                logger,
                problemDetails.Status,
                exception.GetType().FullName ?? exception.GetType().Name);
        }

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception,
            });
    }

    private static ProblemDetails CreateProblemDetails(Exception exception) =>
        exception switch
        {
            AntiforgeryValidationException =>
                Create(
                    StatusCodes.Status403Forbidden,
                    "csrf.invalid",
                    "https://app-core.example/problems/csrf-invalid"),
            ApplicationValidationException validation =>
                new HttpValidationProblemDetails(validation.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "validation.failed",
                    Type = ProblemTypes.Validation,
                },
            ApplicationNotFoundException =>
                Create(
                    StatusCodes.Status404NotFound,
                    "The requested resource was not found.",
                    ProblemTypes.NotFound),
            ApplicationConflictException =>
                Create(
                    StatusCodes.Status409Conflict,
                    "The request conflicts with the current resource state.",
                    ProblemTypes.Conflict),
            _ =>
                Create(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    ProblemTypes.InternalServerError),
        };

    private static ProblemDetails Create(
        int status,
        string title,
        string type) =>
        new()
        {
            Status = status,
            Title = title,
            Type = type,
        };

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled request failure of type {ExceptionType}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string exceptionType);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Request was aborted by the client")]
    private static partial void LogClientAbortedRequest(
        ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Request failed with {StatusCode} and exception type {ExceptionType}")]
    private static partial void LogHandledException(
        ILogger logger,
        int? statusCode,
        string exceptionType);
}
