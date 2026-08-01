using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace AppCore.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    public static bool HasValidConnectionString(DatabaseOptions options)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(
                options.ConnectionString);

            return !string.IsNullOrWhiteSpace(builder.Host)
                && !string.IsNullOrWhiteSpace(builder.Database)
                && !string.IsNullOrWhiteSpace(builder.Username);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
