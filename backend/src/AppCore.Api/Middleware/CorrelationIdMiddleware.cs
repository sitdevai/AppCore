using System.Diagnostics;
using System.Text.RegularExpressions;
using AppCore.Api.Security;

namespace AppCore.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger,
    SensitiveDataRedactor redactor)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context.Request);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (logger.BeginScope(
                   new Dictionary<string, object>
                   {
                       ["CorrelationId"] =
                           redactor.Redact("CorrelationId", correlationId)
                           ?? SensitiveDataRedactor.RedactedValue,
                   }))
        {
            LogHandlingRequest(
                logger,
                context.Request.Method,
                context.Request.Path);

            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpRequest request)
    {
        string? supplied = request.Headers[HeaderName].FirstOrDefault();

        return supplied is not null
               && supplied.Length <= MaximumLength
               && SafeCorrelationIdPattern().IsMatch(supplied)
            ? supplied
            : Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationIdPattern();

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        Message = "Handling {Method} {Path}")]
    private static partial void LogHandlingRequest(
        ILogger logger,
        string method,
        PathString path);
}
