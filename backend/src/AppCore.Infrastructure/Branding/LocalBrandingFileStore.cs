using AppCore.Application.Branding;
using Microsoft.Extensions.Options;

namespace AppCore.Infrastructure.Branding;

public sealed class LocalBrandingFileStore(IOptions<BrandingStorageOptions> options)
    : IBrandingFileStore
{
    private readonly string root = ResolveRoot(options.Value.RootPath);

    public async Task StoreAsync(string storedFileName, Stream content,
        CancellationToken cancellationToken = default)
    {
        string path = Resolve(storedFileName);
        Directory.CreateDirectory(root);
        await using FileStream destination = new(path, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await content.CopyToAsync(destination, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storedFileName,
        CancellationToken cancellationToken = default)
    {
        string path = Resolve(storedFileName);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedFileName,
        CancellationToken cancellationToken = default)
    {
        string path = Resolve(storedFileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storedFileName)
    {
        if (storedFileName != Path.GetFileName(storedFileName))
            throw new InvalidOperationException("Stored branding file name is unsafe.");
        string path = Path.GetFullPath(Path.Combine(root, storedFileName));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Branding file path escaped storage root.");
        return path;
    }

    private static string ResolveRoot(string? configured)
    {
        string value = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "AppCore", "branding-assets")
            : configured;
        return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);
    }
}
