using Microsoft.AspNetCore.Components.Forms;

namespace REMS.Services;

public sealed class FileStorageService
{
    public const long MaxFileSize = 100L * 1024 * 1024;
    private readonly string _rootPath;

    public FileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["FileStorage:RootPath"];

        _rootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "Files")
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(IBrowserFile browserFile, int ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(browserFile);

        if (browserFile.Size is <= 0 or > MaxFileSize)
            throw new InvalidOperationException("The file must be between 1 byte and 100 MB.");

        var ownerDirectory = Path.Combine(_rootPath, "users", ownerId.ToString());
        Directory.CreateDirectory(ownerDirectory);

        var extension = Path.GetExtension(Path.GetFileName(browserFile.Name));
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(ownerDirectory, storedName);

        await using var source = browserFile.OpenReadStream(MaxFileSize, cancellationToken);
        await using var destination = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await source.CopyToAsync(destination, cancellationToken);

        return Path.Combine("users", ownerId.ToString(), storedName).Replace('\\', '/');
    }

    public string GetAbsolutePath(string relativePath)
    {
        var root = Path.GetFullPath(_rootPath);
        var candidate = Path.GetFullPath(Path.Combine(
            _rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage path.");

        return candidate;
    }

    public Task DeleteAsync(string relativePath)
    {
        var path = GetAbsolutePath(relativePath);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
