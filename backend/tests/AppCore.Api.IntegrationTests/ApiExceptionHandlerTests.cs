using AppCore.Api.ErrorHandling;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AppCore.Api.IntegrationTests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task UnhandledExceptionLogsTypeOnlyAndReturnsSafeProblem()
    {
        var problemDetails = new RecordingProblemDetailsService();
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(problemDetails, logger);
        var context = new DefaultHttpContext();
        var exception = new InvalidOperationException("server-only detail");

        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Null(logger.Exception);
        Assert.Equal(LogLevel.Error, logger.Level);
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            problemDetails.Context?.ProblemDetails.Status);
        Assert.Equal(
            "An unexpected error occurred.",
            problemDetails.Context?.ProblemDetails.Title);
        Assert.DoesNotContain(
            exception.Message,
            problemDetails.Context?.ProblemDetails.Title,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientCancellationIsHandledWithoutWritingProblemDetails()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var problemDetails = new RecordingProblemDetailsService();
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(problemDetails, logger);
        var context = new DefaultHttpContext
        {
            RequestAborted = aborted.Token,
        };
        var exception = new OperationCanceledException(aborted.Token);

        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Null(problemDetails.Context);
        Assert.Null(logger.Exception);
        Assert.Equal(LogLevel.Debug, logger.Level);
    }

    [Fact]
    public async Task AntiforgeryFailureReturnsSafeForbiddenProblemDetails()
    {
        var problemDetails = new RecordingProblemDetailsService();
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(problemDetails, logger);
        var context = new DefaultHttpContext();

        bool handled = await handler.TryHandleAsync(
            context,
            new AntiforgeryValidationException("sensitive token detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            problemDetails.Context?.ProblemDetails.Status);
        Assert.Equal("csrf.invalid", problemDetails.Context?.ProblemDetails.Title);
        Assert.Null(logger.Exception);
    }

    private sealed class RecordingProblemDetailsService
        : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(
            ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(
            ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public Exception? Exception { get; private set; }

        public LogLevel? Level { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
        }
    }
}
