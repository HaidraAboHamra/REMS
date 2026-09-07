using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using System.Security.Cryptography;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static REMS.Services.TelegramBotHostedService;
using User = REMS.Enititys.User;

namespace REMS.Services;

public class TelegramService
{
    private readonly TelegramBotClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<TelegramService> _logger;
    private readonly FileStorageService _fileStorage;

    public TelegramService(
        IConfiguration configuration,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<TelegramService> logger,
        FileStorageService fileStorage)
    {
        var token = configuration["TelegramBotToken"];

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "TelegramBotToken غير موجود في appsettings.json");

        _client = new TelegramBotClient(token);
        _dbFactory = dbFactory;
        _logger = logger;
        _fileStorage = fileStorage;
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
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        return await db.Users
            .FirstOrDefaultAsync(
                x => x.ChatId == chatId,
                cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        return await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);
    }

    public async Task<List<User>> GetEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        return await db.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FollowUpReport>> GetUserTasksForDateAsync(
        int userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var start = date.Date;
        var end = start.AddDays(1);

        return await db.FollowUpReports.AsNoTracking()
            .Where(x => x.AssignedEmployeeId == userId
                && !x.IsDone
                && x.DueDate.HasValue
                && x.DueDate.Value >= start
                && x.DueDate.Value < end)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.DueDate)
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

        var normalized =
            NormalizePhone(phoneNumber);

        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var users =
            await db.Users.ToListAsync(
                cancellationToken);

        var user =
            users.FirstOrDefault(
                x => NormalizePhone(x.PhoneNumber) == normalized);

        if (user == null)
            return false;

        user.ChatId = chatId;

        db.Users.Update(user);

        await db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private static string NormalizePhone(
        string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        return new string(
            phone.Where(char.IsDigit).ToArray());
    }

    // =========================================================
    // FILE MANAGER
    // =========================================================
    // Telegram -> FileStorageService -> StoredFile
    //
    // IMPORTANT:
    // This method does NOT create a second storage system.
    // It uses the same FileStorageService and StoredFile
    // used by the existing REMS File Manager.
    // =========================================================

    public async Task<StoredFile?> SaveTelegramFileAsync(
        long chatId,
        Message message,
        int? folderId = null,
        bool isSharedHub = false,
        CancellationToken cancellationToken = default)
    {
        var user =
            await GetUserByChatIdAsync(
                chatId,
                cancellationToken);

        if (user == null)
        {
            _logger.LogWarning(
                "Telegram file upload rejected because chat {ChatId} is not linked to a REMS user.",
                chatId);

            return null;
        }

        string? telegramFileId = null;
        string originalFileName;
        string contentType;
        long fileSize;

        // =====================================================
        // DOCUMENT
        // =====================================================

        if (message.Document != null)
        {
            telegramFileId =
                message.Document.FileId;

            originalFileName =
                Path.GetFileName(
                    string.IsNullOrWhiteSpace(
                        message.Document.FileName)
                        ? $"telegram-{DateTime.UtcNow:yyyyMMddHHmmss}.bin"
                        : message.Document.FileName);

            contentType =
                string.IsNullOrWhiteSpace(
                    message.Document.MimeType)
                    ? "application/octet-stream"
                    : message.Document.MimeType;

            fileSize =
                message.Document.FileSize;
        }

        // =====================================================
        // PHOTO
        // =====================================================

        else if (message.Photo is { Length: > 0 })
        {
            var photo =
                message.Photo
                    .OrderByDescending(
                        x => x.FileSize)
                    .First();

            telegramFileId =
                photo.FileId;

            originalFileName =
                $"photo-{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";

            contentType =
                "image/jpeg";

            fileSize =
                photo.FileSize;
        }

        // =====================================================
        // VIDEO
        // =====================================================

        else if (message.Video != null)
        {
            telegramFileId =
                message.Video.FileId;

            originalFileName =
                Path.GetFileName(
                    string.IsNullOrWhiteSpace(
                        message.Video.FileName)
                        ? $"video-{DateTime.UtcNow:yyyyMMddHHmmss}.mp4"
                        : message.Video.FileName);

            contentType =
                string.IsNullOrWhiteSpace(
                    message.Video.MimeType)
                    ? "video/mp4"
                    : message.Video.MimeType;

            fileSize =
                message.Video.FileSize;
        }

        // =====================================================
        // AUDIO
        // =====================================================

        else if (message.Audio != null)
        {
            telegramFileId =
                message.Audio.FileId;

            originalFileName =
                Path.GetFileName(
                    string.IsNullOrWhiteSpace(
                        message.Audio.FileName)
                        ? $"audio-{DateTime.UtcNow:yyyyMMddHHmmss}.mp3"
                        : message.Audio.FileName);

            contentType =
                string.IsNullOrWhiteSpace(
                    message.Audio.MimeType)
                    ? "audio/mpeg"
                    : message.Audio.MimeType;

            fileSize =
                message.Audio.FileSize;
        }

        // =====================================================
        // ANIMATION / GIF
        // =====================================================

        else if (message.Animation != null)
        {
            telegramFileId =
                message.Animation.FileId;

            originalFileName =
                Path.GetFileName(
                    string.IsNullOrWhiteSpace(
                        message.Animation.FileName)
                        ? $"animation-{DateTime.UtcNow:yyyyMMddHHmmss}.mp4"
                        : message.Animation.FileName);

            contentType =
                string.IsNullOrWhiteSpace(
                    message.Animation.MimeType)
                    ? "video/mp4"
                    : message.Animation.MimeType;

            fileSize =
                message.Animation.FileSize;
        }

        else
        {
            return null;
        }

        // =====================================================
        // BASIC VALIDATION
        // =====================================================

        if (string.IsNullOrWhiteSpace(
                telegramFileId))
        {
            return null;
        }

        if (fileSize <= 0)
        {
            throw new InvalidOperationException(
                "تعذر تحديد حجم الملف القادم من Telegram.");
        }

        if (fileSize >
            FileStorageService.MaxFileSize)
        {
            throw new InvalidOperationException(
                "حجم الملف أكبر من الحد المسموح به في REMS.");
        }

        // =====================================================
        // GET TELEGRAM FILE
        // =====================================================

        Telegram.Bot.Types.File telegramFile;

        try
        {
            telegramFile =
                await _client.GetFileAsync(
                    telegramFileId,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get Telegram file information for {ChatId}",
                chatId);

            throw new InvalidOperationException(
                "تعذر الوصول إلى الملف الموجود في Telegram.",
                ex);
        }

        if (telegramFile == null ||
            string.IsNullOrWhiteSpace(
                telegramFile.FilePath))
        {
            throw new InvalidOperationException(
                "Telegram لم يُرجع مسار الملف.");
        }

        // =====================================================
        // TEMP FILE
        // =====================================================
        // We intentionally do NOT use MemoryStream here.
        // This keeps large files out of RAM.
        // =====================================================

        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "REMS-Telegram");

        Directory.CreateDirectory(
            tempDirectory);

        var tempFileName =
            $"{Guid.NewGuid():N}.tmp";

        var tempFilePath =
            Path.Combine(
                tempDirectory,
                tempFileName);

        try
        {
            // =================================================
            // DOWNLOAD TELEGRAM FILE TO TEMP DISK
            // =================================================

            await using (var tempStream =
                new FileStream(
                    tempFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 128,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan))
            {
                await _client.DownloadFileAsync(
                    telegramFile.FilePath,
                    tempStream,
                    cancellationToken);

                await tempStream.FlushAsync(
                    cancellationToken);
            }

            // =================================================
            // VERIFY ACTUAL FILE SIZE
            // =================================================

            var actualFileSize =
                new FileInfo(tempFilePath)
                    .Length;

            if (actualFileSize <= 0)
            {
                throw new InvalidOperationException(
                    "تم تنزيل ملف فارغ من Telegram.");
            }

            if (actualFileSize >
                FileStorageService.MaxFileSize)
            {
                throw new InvalidOperationException(
                    "الملف الذي تم تنزيله يتجاوز الحد المسموح به في REMS.");
            }

            // =================================================
            // SAVE USING THE EXISTING REMS STORAGE SERVICE
            // =================================================

            string relativePath;

            await using (var source =
                new FileStream(
                    tempFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 128,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan))
            {
                relativePath =
                    await _fileStorage.SaveAsync(
                        source,
                        originalFileName,
                        actualFileSize,
                        user.Id,
                        contentType,
                        cancellationToken);
            }

            // =================================================
            // CREATE THE SAME STORED FILE ENTITY
            // USED BY REMS FILE MANAGER
            // =================================================

            var storedFile =
                new StoredFile
                {
                    OriginalName =
                        originalFileName,

                    StoredName =
                        Path.GetFileName(
                            relativePath),

                    RelativePath =
                        relativePath,

                    ContentType =
                        contentType,

                    Size =
                        actualFileSize,

                    OwnerId =
                        user.Id,

                    FolderId =
                        folderId,

                    IsSharedHub =
                        isSharedHub,

                    CreatedAt =
                        DateTime.UtcNow
                };

            // =================================================
            // SAVE DATABASE RECORD
            // =================================================

            await using var db =
                await _dbFactory.CreateDbContextAsync(
                    cancellationToken);

            db.StoredFiles.Add(
                storedFile);

            await db.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Telegram file saved successfully. UserId={UserId}, FileId={FileId}, FileName={FileName}, Size={Size}",
                user.Id,
                storedFile.Id,
                storedFile.OriginalName,
                storedFile.Size);

            return storedFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save Telegram file for user {UserId}",
                user.Id);

            throw;
        }
        finally
        {
            // =================================================
            // ALWAYS REMOVE TEMP FILE
            // =================================================

            try
            {
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete temporary Telegram file {TempFilePath}",
                    tempFilePath);
            }
        }
    }

    // =========================================================
    // TASKS
    // =========================================================

    public async Task<List<FollowUpReport>> GetUserTasksAsync(
        int userId,
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var query =
            db.FollowUpReports
                .AsNoTracking()
                .Where(
                    x => x.AssignedEmployeeId == userId);

        if (!includeCompleted)
            query =
                query.Where(
                    x => !x.IsDone);

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
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

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
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var creator =
            await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == creatorId,
                    cancellationToken);

        if (creator == null)
            return null;

        User? assignedUser = null;

        if (assignedEmployeeId.HasValue)
        {
            assignedUser =
                await db.Users
                    .FirstOrDefaultAsync(
                        x => x.Id == assignedEmployeeId.Value,
                        cancellationToken);

            if (assignedUser == null)
                return null;
        }

        var task =
            new FollowUpReport
            {
                Content =
                    content.Trim(),

                FullName =
                    creator.FullName,

                DateTime =
                    DateTime.Now,

                AssignedEmployeeId =
                    assignedEmployeeId,

                DueDate =
                    dueDate,

                Priority =
                    priority.ToString(),

                IsDone =
                    false,

                IsDoneOrNot =
                    "لم تبدأ",

                LastUpdatedDate =
                    DateTime.Now,

                CompletedItems =
                    0
            };

        await db.FollowUpReports.AddAsync(
            task,
            cancellationToken);

        await db.SaveChangesAsync(
            cancellationToken);

        return task;
    }

    // =========================================================
    // CREATE EMPLOYEE
    // =========================================================

    public async Task<(bool Success, string Message, User? User)>
        CreateEmployeeAsync(
            string fullName,
            string phoneNumber,
            string email,
            string password,
            NewEmployeeRole role,
            CancellationToken cancellationToken = default)
    {
        fullName =
            fullName.Trim();

        phoneNumber =
            phoneNumber.Trim();

        email =
            email.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            return (
                false,
                "الاسم مطلوب.",
                null);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return (
                false,
                "رقم الهاتف مطلوب.",
                null);

        if (string.IsNullOrWhiteSpace(email))
            return (
                false,
                "البريد الإلكتروني مطلوب.",
                null);

        if (string.IsNullOrWhiteSpace(password))
            return (
                false,
                "كلمة المرور مطلوبة.",
                null);

        if (password.Length < 6)
            return (
                false,
                "كلمة المرور يجب أن تكون 6 أحرف على الأقل.",
                null);

        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var emailExists =
            await db.Users.AnyAsync(
                x => x.Email == email,
                cancellationToken);

        if (emailExists)
            return (
                false,
                "❌ هذا البريد الإلكتروني مستخدم بالفعل.",
                null);

        var phoneExists =
            await db.Users.AnyAsync(
                x => x.PhoneNumber == phoneNumber,
                cancellationToken);

        if (phoneExists)
            return (
                false,
                "❌ رقم الهاتف مستخدم بالفعل.",
                null);

        var user =
            new User
            {
                FullName =
                    fullName,

                PhoneNumber =
                    phoneNumber,

                Email =
                    email,

                IsAdmin =
                    role.IsAdmin,

                IsItAdmin =
                    role.IsItAdmin,

                IsFollowUpAdmin =
                    role.IsFollowUpAdmin,

                IsFUser =
                    role.IsFUser,

                ChatId =
                    null,

                TelegramUsername =
                    null,

                TelegramLinkToken =
                    Convert.ToHexString(
                        RandomNumberGenerator.GetBytes(20))
                    .ToLowerInvariant(),

                TelegramLinkedAt =
                    null
            };

        var passwordHasher =
            new PasswordHasher<User>();

        user.PasswordHash =
            passwordHasher.HashPassword(
                user,
                password);

        db.Users.Add(user);

        await db.SaveChangesAsync(
            cancellationToken);

        return (
            true,
            "✅ تم إنشاء حساب الموظف بنجاح.",
            user
        );
    }

    // =========================================================
    // COMPLETE TASK
    // =========================================================

    public async Task<bool> CompleteTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var requester =
            await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId,
                    cancellationToken);

        if (requester == null)
            return false;

        var task =
            await db.FollowUpReports
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

        task.IsDone =
            true;

        task.IsDoneOrNot =
            "مكتملة";

        task.LastUpdatedDate =
            DateTime.Now;

        if (task.TotalItems > 0)
        {
            task.CompletedItems =
                task.TotalItems;
        }

        task.CompletedDate ??=
            DateTime.Now;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    // =========================================================
    // REOPEN TASK
    // =========================================================

    public async Task<bool> ReopenTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var requester =
            await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId,
                    cancellationToken);

        if (requester == null)
            return false;

        var task =
            await db.FollowUpReports
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

        task.IsDone =
            false;

        task.IsDoneOrNot =
            task.CompletedItems > 0
                ? "قيد التنفيذ"
                : "لم تبدأ";

        task.LastUpdatedDate =
            DateTime.Now;

        task.CompletedDate =
            null;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    // =========================================================
    // UPDATE TASK
    // =========================================================

    public async Task<bool> UpdateTaskContentAsync(
        int taskId,
        int requesterId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var requester =
            await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId,
                    cancellationToken);

        var task =
            await db.FollowUpReports
                .FirstOrDefaultAsync(
                    x => x.Id == taskId,
                    cancellationToken);

        if (requester == null ||
            task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        task.Content =
            content.Trim();

        task.LastUpdatedDate =
            DateTime.Now;

        db.FollowUpReports.Update(task);

        await db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    // =========================================================
    // DELETE TASK
    // =========================================================

    public async Task<bool> DeleteTaskAsync(
        int taskId,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var requester =
            await db.Users
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId,
                    cancellationToken);

        var task =
            await db.FollowUpReports
                .FirstOrDefaultAsync(
                    x => x.Id == taskId,
                    cancellationToken);

        if (requester == null ||
            task == null)
            return false;

        var allowed =
            requester.IsAdmin ||
            requester.IsFollowUpAdmin ||
            task.AssignedEmployeeId == requester.Id;

        if (!allowed)
            return false;

        db.FollowUpReports.Remove(task);

        await db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    // =========================================================
    // ADMIN TASKS
    // =========================================================

    public async Task<List<FollowUpReport>> GetAllOpenTasksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

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
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var today =
            DateTime.Today;

        return await db.FollowUpReports
            .AsNoTracking()
            .Where(
                x =>
                    !x.IsDone &&
                    x.DueDate.HasValue &&
                    x.DueDate.Value.Date < today)
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);
    }

    // =========================================================
    // DAILY REPORT
    // =========================================================

    public async Task<string> BuildDailyReportAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var start =
            date.Date;

        var end =
            start.AddDays(1);

        var tasks =
            await db.FollowUpReports
                .AsNoTracking()
                .Where(
                    x =>
                        (x.DateTime >= start &&
                         x.DateTime < end) ||
                        (x.LastUpdatedDate.HasValue &&
                         x.LastUpdatedDate.Value >= start &&
                         x.LastUpdatedDate.Value < end))
                .OrderBy(x => x.DueDate)
                .ToListAsync(cancellationToken);

        var users =
            await db.Users
                .AsNoTracking()
                .ToDictionaryAsync(
                    x => x.Id,
                    cancellationToken);

        var completed =
            tasks.Count(x => x.IsDone);

        var pending =
            tasks.Count(x => !x.IsDone);

        var lines =
            new List<string>
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
            string employee =
                task.AssignedEmployeeId.HasValue &&
                users.TryGetValue(
                    task.AssignedEmployeeId.Value,
                    out var u)
                    ? u.FullName ?? "غير معروف"
                    : task.FullName ?? "غير محدد";

            lines.Add(
                $"{(task.IsDone ? "✅" : "⏳")} #{task.Id} - {task.Content}");

            lines.Add(
                $"👤 الموظف: {employee}");

            if (task.DueDate.HasValue)
            {
                lines.Add(
                    $"📅 الموعد: {task.DueDate:yyyy-MM-dd}");
            }

            lines.Add(
                $"📍 الحالة: {task.IsDoneOrNot ?? "غير محددة"}");

            lines.Add("");
        }

        return string.Join(
            "\n",
            lines);
    }

    // =========================================================
    // WEEKLY REPORT
    // =========================================================

    public async Task<string> BuildWeeklyReportAsync(
        DateTime weekStart,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        weekStart =
            weekStart.Date;

        var weekEnd =
            weekStart.AddDays(7);

        var tasks =
            await db.FollowUpReports
                .AsNoTracking()
                .Where(
                    x =>
                        (x.DateTime >= weekStart &&
                         x.DateTime < weekEnd) ||
                        (x.LastUpdatedDate.HasValue &&
                         x.LastUpdatedDate.Value >= weekStart &&
                         x.LastUpdatedDate.Value < weekEnd) ||
                        (x.DueDate.HasValue &&
                         x.DueDate.Value >= weekStart &&
                         x.DueDate.Value < weekEnd))
                .OrderBy(x => x.DueDate)
                .ToListAsync(cancellationToken);

        var completed =
            tasks.Count(x => x.IsDone);

        var open =
            tasks.Count(x => !x.IsDone);

        var lines =
            new List<string>
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
            {
                lines.Add(
                    $"📅 الموعد: {task.DueDate:yyyy-MM-dd}");
            }

            lines.Add(
                $"الحالة: {task.IsDoneOrNot ?? "غير محددة"}");

            lines.Add("");
        }

        return string.Join(
            "\n",
            lines);
    }

    // =========================================================
    // BACKLOG REPORT
    // =========================================================

    public async Task<string> BuildBacklogReportAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var today =
            DateTime.Today;

        var tasks =
            await db.FollowUpReports
                .AsNoTracking()
                .Where(x => !x.IsDone)
                .OrderBy(x => x.DueDate)
                .ThenByDescending(x => x.Priority)
                .ToListAsync(cancellationToken);

        var overdue =
            tasks.Count(
                x =>
                    x.DueDate.HasValue &&
                    x.DueDate.Value.Date < today);

        var withoutDueDate =
            tasks.Count(
                x => !x.DueDate.HasValue);

        var lines =
            new List<string>
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
            {
                lines.Add(
                    $"📅 الموعد: {task.DueDate:yyyy-MM-dd}");
            }

            lines.Add("");
        }

        return string.Join(
            "\n",
            lines);
    }

    // =========================================================
    // DAILY NOTIFICATION
    // =========================================================

    public async Task SendDailyAssignedTasks(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var users =
            await db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.ChatId.HasValue &&
                        x.ChatId.Value > 0)
                .ToListAsync(cancellationToken);

        var userIds =
            users
                .Select(x => x.Id)
                .ToList();

        var tasks =
            await db.FollowUpReports
                .AsNoTracking()
                .Where(
                    x =>
                        x.AssignedEmployeeId.HasValue &&
                        userIds.Contains(
                            x.AssignedEmployeeId.Value) &&
                        !x.IsDone)
                .OrderBy(x => x.DueDate)
                .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var assignedTasks =
                tasks
                    .Where(
                        x =>
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
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var chatIds =
            await db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.ChatId.HasValue &&
                        x.ChatId.Value > 0)
                .Select(
                    x => x.ChatId!.Value)
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
