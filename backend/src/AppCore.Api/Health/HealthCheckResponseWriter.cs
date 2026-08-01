using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppCore.Api.Health;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                }),
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}
