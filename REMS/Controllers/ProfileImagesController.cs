using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using System.Security.Claims;

namespace REMS.Controllers;

[Authorize]
[Route("profile-images")]
public sealed class ProfileImagesController : Controller
{
    private readonly AppDbContext _db;
    private readonly Services.ProfileImageService _images;

    public ProfileImagesController(AppDbContext db, Services.ProfileImageService images)
    {
        _db = db;
        _images = images;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (!int.TryParse(User.FindFirstValue("Id"), out var actorId)) return Forbid();
        var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorId);
        var target = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsFUser);
        if (target?.ProfileImagePath is null || (actor?.Id != target.Id && actor?.IsFollowUpAdmin != true && actor?.IsAdmin != true))
            return NotFound();

        try
        {
            var path = _images.GetAbsolutePath(target.ProfileImagePath);
            return System.IO.File.Exists(path) ? PhysicalFile(path, target.ProfileImageContentType ?? "image/jpeg") : NotFound();
        }
        catch (InvalidOperationException) { return NotFound(); }
    }
}