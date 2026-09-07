using Microsoft.AspNetCore.Components.Forms;

namespace REMS.Services;

public sealed class ProfileImageService
{
    private const long MaxSize = 5 * 1024 * 1024;
    private readonly string _rootPath;

    public ProfileImageService(IWebHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "ProfileImages");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<(string RelativePath, string ContentType)> SaveAsync(IBrowserFile file, int userId, CancellationToken cancellationToken = default)
    {
        if (file.Size is <= 0 or > MaxSize)
            throw new InvalidOperationException("الصورة يجب ألا تتجاوز 5 ميغابايت.");

        var contentType = file.ContentType.ToLowerInvariant();
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("يسمح بصور JPG أو PNG أو WebP فقط.")
        };

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(_rootPath, $"{userId}_{storedName}");
        await using var input = file.OpenReadStream(MaxSize, cancellationToken);
        await using var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
        return ($"{userId}_{storedName}", contentType);
    }

    public string GetAbsolutePath(string relativePath)
    {
        var root = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, Path.GetFileName(relativePath)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid profile image path.");
        return candidate;
    }
}