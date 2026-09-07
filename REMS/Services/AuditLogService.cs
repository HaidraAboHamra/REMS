using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using System.Security.Claims;

namespace REMS.Services;

public sealed class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditLogService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task WriteAsync(string action, string description, int? targetUserId = null, string? entityType = null, string? entityId = null, CancellationToken cancellationToken = default)
    {
        var context = _http.HttpContext;
        int? actorId = int.TryParse(context?.User.FindFirstValue("Id"), out var id) ? id : null;
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            TargetUserId = targetUserId,
            Action = action,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context?.Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<AuditLog>> GetForUserAsync(int userId) =>
        _db.AuditLogs.AsNoTracking().Where(x => x.TargetUserId == userId && x.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
            .OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task RemoveExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        await _db.AuditLogs.Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}