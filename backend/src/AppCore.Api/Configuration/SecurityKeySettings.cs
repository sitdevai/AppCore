namespace AppCore.Api.Configuration;

public sealed class SecurityKeySettings
{
    public const string SectionName = "SecurityKeys";

    public int ChallengeHmacKeyVersion { get; init; } = 1;
    public string? ChallengeHmacKeyBase64 { get; init; }
    public int CurrentVersion { get; init; }
    public Dictionary<int, string> Keys { get; init; } = [];

    public static bool HasProductionKey(SecurityKeySettings settings)
    {
        int currentVersion = settings.CurrentVersion > 0
            ? settings.CurrentVersion
            : settings.ChallengeHmacKeyVersion;
        Dictionary<int, string> keys = settings.Keys.Count > 0
            ? settings.Keys
            : new Dictionary<int, string>
            {
                [settings.ChallengeHmacKeyVersion] =
                    settings.ChallengeHmacKeyBase64 ?? string.Empty,
            };
        if (currentVersion < 1
            || !keys.ContainsKey(currentVersion)
            || keys.Count == 0)
        {
            return false;
        }

        try
        {
            return keys.All(value =>
                value.Key > 0
                && Convert.FromBase64String(value.Value).Length >= 32);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
