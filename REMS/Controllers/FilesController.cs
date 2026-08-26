using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.Services;
using System.Security.Claims;

namespace REMS.Controllers;

[Authorize]
[Route("files-api")]
public class FilesController : Controller
{
    private readonly AppDbContext _db;
    private readonly FileStorageService _storage;

    public FilesController(AppDbContext db, FileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet("download/{id:long}")]
    public Task<IActionResult> Download(long id) => Serve(id, false);

    [HttpGet("preview/{id:long}")]
    public Task<IActionResult> Preview(long id) => Serve(id, true);

    [AllowAnonymous]
    [HttpGet("share/{token}")]
    public async Task<IActionResult> PublicShare(string token)
    {
        var link = await _db.Set<FileShareLink>()
            .AsNoTracking()
            .Include(x => x.File)
            .FirstOrDefaultAsync(x => x.Token == token && !x.IsRevoked);

        if (link?.File is null || (link.ExpiresAt.HasValue && link.ExpiresAt.Value <= DateTime.UtcNow))
            return NotFound();

        var path = _storage.GetAbsolutePath(link.File.RelativePath);
        if (!System.IO.File.Exists(path)) return NotFound();

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
        return File(stream, link.File.ContentType ?? "application/octet-stream", link.File.OriginalName, enableRangeProcessing: true);
    }

    private async Task<IActionResult> Serve(long id, bool inline)
    {
        var userId = GetUserId();

        var file = await _db.Set<StoredFile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (file is null || !await CanRead(file, userId)) return NotFound();

        var path = _storage.GetAbsolutePath(file.RelativePath);
        if (!System.IO.File.Exists(path)) return NotFound();

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
        return File(stream, file.ContentType ?? "application/octet-stream", inline ? null : file.OriginalName, enableRangeProcessing: true);
    }

    private async Task<bool> CanRead(StoredFile file, int userId)
    {
        if (file.OwnerId == userId) return true;
        return await _db.Set<FilePermission>().AnyAsync(x => x.FileId == file.Id && x.UserId == userId);
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue("Id");
        if (!int.TryParse(value, out var userId) || userId <= 0)
            throw new UnauthorizedAccessException("Invalid user identity.");
        return userId;
    }
}
