using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = REMS.Enititys.User;

namespace REMS.Services;

public class TelegramService
{
    private readonly TelegramBotClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(
        IConfiguration configuration,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<TelegramService> logger)
    {
        var token = configuration["TelegramBotToken"];

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "TelegramBotToken غير موجود في appsettings.json");

        _client = new TelegramBotClient(token);
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public TelegramBotClient Client => _client;

    // =========================================================
    // BASIC SEND
    // =========================================================

    public async Task SendMessageAsync(
        long chatId,
        string message,
        InlineKeyboardMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Telegram message to {ChatId}",
                chatId);
        }
    }

    // =========================================================
    // USER LOOKUP
    // =========================================================

    public async Task<User?> GetUserByChatIdAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .FirstOrDefaultAsync(
                x => x.ChatId == chatId,
                cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);
    }

    public async Task<List<User>> GetEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }

    // =========================================================
    // LINK TELEGRAM ACCOUNT USING PHONE CONTACT
    // =========================================================

    public async Task<bool> LinkTelegramAccountAsync(
        long chatId,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var normalized = NormalizePhone(phoneNumber);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users
            .ToListAsync(cancellationToken);

        var user = users.FirstOrDefault(x =>
            NormalizePhone(x.PhoneNumber) == normalized);

        if (user == null)
            return false;

        user.ChatId = chatId;

        db.Users.Update(user);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        return new string(
            phone.Where(char.IsDigit).ToArray());
    }

    // =========================================================
    // TASKS
    // =========================================================

    public async Task<List<FollowUpReport>> GetUserTasksAsync(
        int userId,
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.FollowUpReports
            .AsNoTracking()
            .Where(x => x.AssignedEmployeeId == userId);

        if (!includeCompleted)
            query = query.Where(x => !x.IsDone);

        return await query
            .OrderBy(x => x.IsDone)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<FollowUpReport?> GetTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.FollowUpReports
            .FirstOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken);
    }

    public async Task<FollowUpReport?> CreateTaskAsync(
        int creatorId,
        int? assignedEmployeeId,
        string content,
        DateTime? dueDate,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var creator = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == creatorId,
                cancellationToken);

        if (creator == null)
            return null;

        User? assignedUser = null;

        if (assignedEmployeeId.HasValue)
        {
            assignedUser = await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == assignedEmployeeId.Value,
                    cancellationToken);

            if (assignedUser == null)
                return null;
        }

        var task = new FollowUpReport
        {
            Content = content.Trim(),
            FullName = creator.FullName,
            DateTime = DateTime.Now,

            AssignedEmployeeId = assignedEmployeeId,
            DueDate = dueDate,

            Priority = priority.ToString(),

            IsDone = false,
            IsDoneOrNot = "لم تبدأ",
            LastUpdatedDate = DateTime.Now,
            CompletedItems = 0
        };

        await db.FollowUpReports.AddAsync(
            task,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return task;
    }

    public async Task<bool> CompleteTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var requester = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == requesterId,
                cancellationToken);

        if (requester == null)
            return false;

        var task = await db.FollowUpReports
            .FirstOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken);

        if (task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        task.IsDone = true;
        task.IsDoneOrNot = "مكتملة";
        task.LastUpdatedDate = DateTime.Now;

        if (task.TotalItems > 0)
        {
            task.CompletedItems = task.TotalItems;
        }

        task.CompletedDate ??= DateTime.Now;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ReopenTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var requester = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == requesterId,
                cancellationToken);

        if (requester == null)
            return false;

        var task = await db.FollowUpReports
            .FirstOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken);

        if (task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        task.IsDone = false;

        task.IsDoneOrNot =
            task.CompletedItems > 0
                ? "قيد التنفيذ"
                : "لم تبدأ";

        task.LastUpdatedDate = DateTime.Now;
        task.CompletedDate = null;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UpdateTaskContentAsync(
        int taskId,
        int requesterId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var requester = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == requesterId,
                cancellationToken);

        var task = await db.FollowUpReports
            .FirstOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken);

        if (requester == null || task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        task.Content = content.Trim();
        task.LastUpdatedDate = DateTime.Now;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var requester = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == requesterId,
                cancellationToken);

        var task = await db.FollowUpReports
            .FirstOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken);

        if (requester == null || task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        db.FollowUpReports.Remove(task);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    // =========================================================
    // ADMIN TASKS
    // =========================================================

    public async Task<List<FollowUpReport>> GetAllOpenTasksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.FollowUpReports
            .AsNoTracking()
            .Where(x => !x.IsDone)
            .OrderBy(x => x.DueDate)
            .ThenByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FollowUpReport>> GetOverdueTasksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var today = DateTime.Today;

        return await db.FollowUpReports
            .AsNoTracking()
            .Where(x =>
                !x.IsDone &&
                x.DueDate.HasValue &&
                x.DueDate.Value.Date < today)
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);
    }

    // =========================================================
    // REPORTS
    // =========================================================

    public async Task<string> BuildDailyReportAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var start = date.Date;
        var end = start.AddDays(1);

        var tasks = await db.FollowUpReports
            .AsNoTracking()
            .Where(x =>
                (x.DateTime >= start && x.DateTime < end) ||
                (x.LastUpdatedDate.HasValue &&
                 x.LastUpdatedDate.Value >= start &&
                 x.LastUpdatedDate.Value < end))
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        var completed = tasks.Count(x => x.IsDone);
        var pending = tasks.Count(x => !x.IsDone);

        var lines = new List<string>
        {
            "📊 تقرير REMS اليومي",
            $"📅 التاريخ: {start:yyyy-MM-dd}",
            "",
            $"📌 إجمالي المهام: {tasks.Count}",
            $"✅ المكتملة: {completed}",
            $"⏳ غير المكتملة: {pending}",
            ""
        };

        foreach (var task in tasks)
        {
            string employee = task.AssignedEmployeeId.HasValue &&
                               users.TryGetValue(
                                   task.AssignedEmployeeId.Value,
                                   out var u)
                ? u.FullName ?? "غير معروف"
                : task.FullName ?? "غير محدد";

            lines.Add(
                $"{(task.IsDone ? "✅" : "⏳")} #{task.Id} - {task.Content}");

            lines.Add($"👤 الموظف: {employee}");

            if (task.DueDate.HasValue)
                lines.Add($"📅 الموعد: {task.DueDate:yyyy-MM-dd}");

            lines.Add($"📍 الحالة: {task.IsDoneOrNot ?? "غير محددة"}");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    public async Task<string> BuildWeeklyReportAsync(
        DateTime weekStart,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        weekStart = weekStart.Date;
        var weekEnd = weekStart.AddDays(7);

        var tasks = await db.FollowUpReports
            .AsNoTracking()
            .Where(x =>
                (x.DateTime >= weekStart && x.DateTime < weekEnd) ||
                (x.LastUpdatedDate.HasValue &&
                 x.LastUpdatedDate.Value >= weekStart &&
                 x.LastUpdatedDate.Value < weekEnd) ||
                (x.DueDate.HasValue &&
                 x.DueDate.Value >= weekStart &&
                 x.DueDate.Value < weekEnd))
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);

        var completed = tasks.Count(x => x.IsDone);
        var open = tasks.Count(x => !x.IsDone);

        var lines = new List<string>
        {
            "📊 تقرير REMS الأسبوعي",
            $"📅 من {weekStart:yyyy-MM-dd}",
            $"📅 إلى {weekEnd.AddDays(-1):yyyy-MM-dd}",
            "",
            $"📌 إجمالي المهام: {tasks.Count}",
            $"✅ المكتملة: {completed}",
            $"⏳ غير المكتملة: {open}",
            ""
        };

        foreach (var task in tasks)
        {
            lines.Add(
                $"{(task.IsDone ? "✅" : "⏳")} #{task.Id} - {task.Content}");

            if (task.DueDate.HasValue)
                lines.Add($"📅 الموعد: {task.DueDate:yyyy-MM-dd}");

            lines.Add($"الحالة: {task.IsDoneOrNot ?? "غير محددة"}");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    public async Task<string> BuildBacklogReportAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var today = DateTime.Today;

        var tasks = await db.FollowUpReports
            .AsNoTracking()
            .Where(x => !x.IsDone)
            .OrderBy(x => x.DueDate)
            .ThenByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);

        var overdue = tasks.Count(x =>
            x.DueDate.HasValue &&
            x.DueDate.Value.Date < today);

        var withoutDueDate = tasks.Count(x =>
            !x.DueDate.HasValue);

        var lines = new List<string>
        {
            "⚠️ تقرير المهام المتراكمة",
            "",
            $"📌 جميع المهام المفتوحة: {tasks.Count}",
            $"🔴 المتأخرة: {overdue}",
            $"🟡 بدون موعد: {withoutDueDate}",
            ""
        };

        foreach (var task in tasks)
        {
            var status =
                task.DueDate.HasValue &&
                task.DueDate.Value.Date < today
                    ? "🔴"
                    : "🟡";

            lines.Add(
                $"{status} #{task.Id} - {task.Content}");

            if (task.DueDate.HasValue)
                lines.Add(
                    $"📅 الموعد: {task.DueDate:yyyy-MM-dd}");

            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    // =========================================================
    // DAILY NOTIFICATION
    // =========================================================

    public async Task SendDailyAssignedTasks(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(x =>
                x.ChatId.HasValue &&
                x.ChatId.Value > 0)
            .ToListAsync(cancellationToken);

        var userIds = users
            .Select(x => x.Id)
            .ToList();

        var tasks = await db.FollowUpReports
            .AsNoTracking()
            .Where(x =>
                x.AssignedEmployeeId.HasValue &&
                userIds.Contains(x.AssignedEmployeeId.Value) &&
                !x.IsDone)
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var assignedTasks = tasks
                .Where(x =>
                    x.AssignedEmployeeId == user.Id)
                .ToList();

            var message =
                assignedTasks.Count == 0
                    ? $"🌅 صباح الخير {user.FullName}.\nلا توجد مهام غير مكتملة مكلفة لك حاليًا."
                    : $"🌅 صباح الخير {user.FullName}.\n" +
                      $"📌 لديك {assignedTasks.Count} مهمة غير مكتملة.\n\n" +
                      string.Join(
                          "\n\n",
                          assignedTasks.Select(
                              (task, index) =>
                                  $"{index + 1}. #{task.Id} {task.Content}\n" +
                                  $"الحالة: {task.IsDoneOrNot ?? "لم تبدأ"}\n" +
                                  $"الموعد: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد")}"));

            try
            {
                await _client.SendTextMessageAsync(
                    chatId: user.ChatId!.Value,
                    text: message,
                    cancellationToken: cancellationToken);
            }
            catch (ChatNotFoundException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Telegram chat not found for user {UserId}",
                    user.Id);
            }
        }
    }

    // =========================================================
    // BROADCAST
    // =========================================================

    public async Task SendMessagesToAllUsers(
        string message,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(cancellationToken);

        var chatIds = await db.Users
            .AsNoTracking()
            .Where(x =>
                x.ChatId.HasValue &&
                x.ChatId.Value > 0)
            .Select(x => x.ChatId!.Value)
            .ToListAsync(cancellationToken);

        foreach (var chatId in chatIds)
        {
            try
            {
                await _client.SendTextMessageAsync(
                    chatId,
                    message,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Broadcast failed for {ChatId}",
                    chatId);
            }
        }
    }
}