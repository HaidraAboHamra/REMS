using Microsoft.EntityFrameworkCore;
using REMS.Authorization;
using REMS.Data;
using REMS.DTOs;
using REMS.Enititys;

namespace REMS.Services;

public sealed class AdminCenterService
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public AdminCenterService(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AdminDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var totalTasks = await _db.FollowUpReports.CountAsync(cancellationToken);
        var completedTasks = await _db.FollowUpReports.CountAsync(x => x.IsDone, cancellationToken);
        var openTasks = totalTasks - completedTasks;
        var overdueTasks = await _db.FollowUpReports.CountAsync(
            x => !x.IsDone && x.DueDate.HasValue && x.DueDate.Value.Date < today,
            cancellationToken);

        return new AdminDashboardSnapshot(
            await _db.Users.CountAsync(cancellationToken),
            await _db.Users.CountAsync(x => x.ChatId.HasValue, cancellationToken),
            totalTasks,
            openTasks,
            overdueTasks,
            completedTasks,
            await _db.Users.CountAsync(x => x.IsActive, cancellationToken),
            await _db.Users.CountAsync(x => !x.IsActive, cancellationToken),
            await _db.Users.CountAsync(x => !x.ChatId.HasValue, cancellationToken),
            DateTime.UtcNow);
    }

    public Task<TelegramBotSettings> GetTelegramSettingsAsync(CancellationToken cancellationToken = default) =>
        _db.TelegramBotSettings.SingleAsync(cancellationToken);

    public async Task<IReadOnlyList<TelegramDeliveryLogDto>> GetTelegramDeliveryLogsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        await _db.TelegramDeliveryLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(x => new TelegramDeliveryLogDto(x.Id, x.Operation, x.Status, x.ErrorMessage, x.CreatedAt, x.ChatId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> SearchAuditAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Action.Contains(term) || x.Description.Contains(term) || (x.EntityType ?? "").Contains(term));
        }
        return await query.OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync(cancellationToken);
    }

    public async Task<(bool Database, int AuditEntries)> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var database = await _db.Database.CanConnectAsync(cancellationToken);
        var entries = database ? await _db.AuditLogs.CountAsync(cancellationToken) : 0;
        return (database, entries);
    }

    public async Task<AdminDecisionSummary> GetDecisionSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var atRisk = await _db.FollowUpReports.CountAsync(x => !x.IsDone && x.DueDate.HasValue && x.DueDate.Value.Date <= today.AddDays(2), cancellationToken);
        var critical = await _db.FollowUpReports.CountAsync(x => !x.IsDone && x.DueDate.HasValue && x.DueDate.Value.Date < today, cancellationToken);
        var unlinked = await _db.Users.CountAsync(x => x.IsActive && !x.ChatId.HasValue, cancellationToken);
        var recommendation = critical > 0
            ? "ابدأ بالمهام المتأخرة ثم فعّل تنبيهات Telegram للحسابات غير المرتبطة."
            : atRisk > 0
                ? "راجع المهام القريبة من موعد التسليم ووزّعها قبل تحولها إلى متأخرة."
                : "الوضع التشغيلي مستقر؛ راقب مؤشرات الإنجاز بشكل دوري.";
        return new AdminDecisionSummary(atRisk, critical, unlinked, recommendation);
    }

    public async Task<IReadOnlyList<AdminPermissionMatrixRow>> GetPermissionMatrixAsync(
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken = default)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(x => x.IsAdmin || x.IsFollowUpAdmin || x.IsItAdmin)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName })
            .ToListAsync(cancellationToken);

        var assignments = await _db.AdminPermissionAssignments.AsNoTracking()
            .Where(x => users.Select(user => user.Id).Contains(x.UserId))
            .ToListAsync(cancellationToken);

        return users.Select(user =>
        {
            var assigned = assignments.Where(x => x.UserId == user.Id)
                .ToDictionary(x => x.PermissionKey, x => x.IsGranted, StringComparer.OrdinalIgnoreCase);
            var permissions = permissionKeys.ToDictionary(
                key => key,
                key => assigned.TryGetValue(key, out var granted) && granted,
                StringComparer.OrdinalIgnoreCase);
            return new AdminPermissionMatrixRow(user.Id, user.FullName ?? $"User #{user.Id}", permissions);
        }).ToList();
    }

    public async Task UpdateTelegramSettingsAsync(
        TelegramBotSettings settings,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var current = await _db.TelegramBotSettings.SingleAsync(cancellationToken);
        current.IsEnabled = settings.IsEnabled;
        current.NotificationsEnabled = settings.NotificationsEnabled;
        current.BroadcastEnabled = settings.BroadcastEnabled;
        current.AllowTaskCreation = settings.AllowTaskCreation;
        current.AllowTaskEditing = settings.AllowTaskEditing;
        current.AllowTaskDeletion = settings.AllowTaskDeletion;
        current.MaxBroadcastRecipients = Math.Clamp(settings.MaxBroadcastRecipients, 1, 10_000);
        current.RetryCount = Math.Clamp(settings.RetryCount, 0, 10);
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(AdminAuditActions.SettingsUpdated, "تم تحديث إعدادات بوت Telegram.", actorUserId, nameof(TelegramBotSettings), current.Id.ToString(), cancellationToken);
    }

    public async Task SetPermissionAsync(
        int userId,
        string permissionKey,
        bool isGranted,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        var assignment = await _db.AdminPermissionAssignments
            .SingleOrDefaultAsync(x => x.UserId == userId && x.PermissionKey == permissionKey, cancellationToken);

        if (assignment is null)
        {
            assignment = new AdminPermissionAssignment
            {
                UserId = userId,
                PermissionKey = permissionKey.Trim()
            };
            _db.AdminPermissionAssignments.Add(assignment);
        }

        assignment.IsGranted = isGranted;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(AdminAuditActions.PermissionChanged, $"تم تغيير صلاحية {permissionKey} للمستخدم {userId}.", actorUserId, nameof(AdminPermissionAssignment), assignment.Id.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> SearchUsersAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => (x.FullName ?? "").Contains(term) || (x.Email ?? "").Contains(term));
        }

        return await query.OrderBy(x => x.FullName).Take(200).ToListAsync(cancellationToken);
    }

    public async Task SetUserActiveAsync(int userId, bool isActive, int actorUserId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (user.Id == actorUserId && !isActive)
            throw new InvalidOperationException("لا يمكن تعطيل الحساب المستخدم حاليًا.");

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(AdminAuditActions.UserStatusChanged, $"تم {(isActive ? "تفعيل" : "تعطيل")} المستخدم {user.Email}.", user.Id, nameof(User), user.Id.ToString(), cancellationToken);
    }

    public async Task<int> CompleteOverdueTasksAsync(int actorUserId, CancellationToken cancellationToken = default)
    {
        var overdue = await _db.FollowUpReports
            .Where(x => !x.IsDone && x.DueDate.HasValue && x.DueDate.Value.Date < DateTime.Today)
            .ToListAsync(cancellationToken);

        foreach (var task in overdue)
        {
            task.IsDoneOrNot = "متأخرة";
            task.LastUpdatedDate = DateTime.Now;
            task.LastUpdatedBy = $"Admin #{actorUserId}";
        }

        if (overdue.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("AdminOverdueTasksReviewed", $"تمت مراجعة {overdue.Count} مهمة متأخرة.", actorUserId, nameof(FollowUpReport), cancellationToken: cancellationToken);
        return overdue.Count;
    }
}
