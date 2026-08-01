namespace AppCore.Api.Configuration;

public sealed class LoggingRedactionSettings
{
    public const string SectionName = "LoggingRedaction";

    public string[] SensitiveKeys { get; init; } =
    [
        "authorization",
        "cookie",
        "password",
        "secret",
        "token",
        "connectionstring",
    ];
}
