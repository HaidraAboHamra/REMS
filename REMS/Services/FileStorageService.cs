using Microsoft.AspNetCore.Components.Forms;

namespace REMS.Services;

public sealed class FileStorageService
{
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
        var ownerDirectory = Path.Combine(_rootPath, "users", ownerId.ToString());
        Directory.CreateDirectory(ownerDirectory);

        var extension = Path.GetExtension(browserFile.Name);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(ownerDirectory, storedName);

        await using var source = browserFile.OpenReadStream(500L * 1024 * 1024, cancellationToken);
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
