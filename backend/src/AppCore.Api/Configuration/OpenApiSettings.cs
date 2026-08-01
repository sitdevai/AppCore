namespace AppCore.Api.Configuration;

public sealed class OpenApiSettings
{
    public const string SectionName = "OpenApi";

    public bool EnableInProduction { get; init; }
}
