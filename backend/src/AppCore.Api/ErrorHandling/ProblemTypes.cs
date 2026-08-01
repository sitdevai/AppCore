namespace AppCore.Api.ErrorHandling;

public static class ProblemTypes
{
    private const string Rfc9110 =
        "https://www.rfc-editor.org/rfc/rfc9110";

    public const string Validation =
        $"{Rfc9110}#name-400-bad-request";
    public const string NotFound =
        $"{Rfc9110}#name-404-not-found";
    public const string Conflict =
        $"{Rfc9110}#name-409-conflict";
    public const string TooManyRequests =
        $"{Rfc9110}#name-429-too-many-requests";
    public const string InternalServerError =
        $"{Rfc9110}#name-500-internal-server-error";
}
