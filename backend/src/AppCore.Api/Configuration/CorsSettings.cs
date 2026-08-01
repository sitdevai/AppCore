namespace AppCore.Api.Configuration;

public sealed class CorsSettings
{
    public const string PolicyName = "WebClient";
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
