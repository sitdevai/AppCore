using AppCore.Api;
using AppCore.Api.Configuration;
using AppCore.Api.Endpoints;
using AppCore.Api.Health;
using AppCore.Api.Middleware;
using AppCore.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(CorsSettings.PolicyName);
app.UseAuthentication();
app.UseMiddleware<AuthenticatedSessionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<SessionActivityMiddleware>();

OpenApiSettings openApi =
    app.Services.GetRequiredService<IOptions<OpenApiSettings>>().Value;
if (app.Environment.IsDevelopment() || openApi.EnableInProduction)
{
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
}

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    }).AllowAnonymous();
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    }).AllowAnonymous();
app.MapSystemEndpoints();
app.MapAuthenticationEndpoints();
app.MapAdministrationEndpoints();
app.MapSecurityAdministrationEndpoints();
app.MapBrandingEndpoints();
app.Map("/{**path}", () => Results.NotFound()).AllowAnonymous();

app.Run();

public partial class Program;
