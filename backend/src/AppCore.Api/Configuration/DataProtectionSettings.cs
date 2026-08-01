using System.ComponentModel.DataAnnotations;

namespace AppCore.Api.Configuration;

public sealed class DataProtectionSettings
{
    public const string SectionName = "DataProtection";

    [Required]
    public string ApplicationName { get; init; } = "AppCore";

    public string KeyStoragePath { get; init; } = string.Empty;

    public string CertificateThumbprint { get; init; } = string.Empty;

    public static bool HasProductionProtection(DataProtectionSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ApplicationName)
        && Path.IsPathFullyQualified(settings.KeyStoragePath)
        && !string.IsNullOrWhiteSpace(settings.CertificateThumbprint);
}
