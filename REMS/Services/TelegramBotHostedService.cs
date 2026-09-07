using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace REMS.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly TelegramService _telegram;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private readonly ConcurrentDictionary<long, BotState> _states = new();

    public TelegramBotHostedService(
        TelegramService telegram,
        ILogger<TelegramBotHostedService> logger,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        _telegram = telegram;
        _logger = logger;
        _dbFactory = dbFactory;
    }

    private async Task<TelegramBotSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TelegramBotSettings.AsNoTracking().SingleAsync(cancellationToken);
    }

    // =========================================================
    // ARABIC MONTH NAME
    // =========================================================

    private static string GetArabicMonthName(
        int month)
    {
        var settings = await GetSettingsAsync();
        if (!settings.IsEnabled)
        {
            await _telegram.SendMessageAsync(user.ChatId!.Value, "⛔ بوت Telegram متوقف مؤقتًا من لوحة الإدارة.");
            return;
        }
        return month switch
        {
            1 => "يناير",
            2 => "فبراير",
            3 => "مارس",
            4 => "أبريل",
            5 => "مايو",
            6 => "يونيو",
            7 => "يوليو",
            8 => "أغسطس",
            9 => "سبتمبر",
            10 => "أكتوبر",
            11 => "نوفمبر",
            12 => "ديسمبر",
            _ => string.Empty
        };
    }

    // =========================================================
    // EXECUTE
    // =========================================================

    [Obsolete]
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "REMS Telegram Bot is starting...");

        var allowedUpdates = new[]
        {
            UpdateType.Message,
            UpdateType.CallbackQuery
        };

        try
        {
            _telegram.Client.OnUpdate += OnUpdateReceived;

            _telegram.Client.StartReceiving(
                allowedUpdates,
                stoppingToken);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Application is stopping.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram Bot failed.");
        }
        finally
        {
            _telegram.Client.OnUpdate -= OnUpdateReceived;

            _telegram.Client.StopReceiving();

            _logger.LogInformation(
                "REMS Telegram Bot stopped.");
        }
    }

    // =========================================================
    // TELEGRAM UPDATE
    // =========================================================

    private void OnUpdateReceived(
        object? sender,
        UpdateEventArgs e)
    {
        _ = ProcessUpdateAsync(
            e.Update);
    }

    private async Task ProcessUpdateAsync(
        Telegram.Bot.Types.Update update)
    {
        try
        {
            if (update.Type == UpdateType.Message &&
                update.Message != null)
            {
                await HandleMessageAsync(
                    update.Message);

                return;
            }

            if (update.Type == UpdateType.CallbackQuery &&
                update.CallbackQuery != null)
            {
                await HandleCallbackAsync(
                    update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Telegram update.");
        }
    }

    // =========================================================
    // MESSAGE
    // =========================================================

    private async Task HandleMessageAsync(
        Telegram.Bot.Types.Message message)
    {
        var chatId =
            message.Chat.Id;

        // -----------------------------------------------------
        // TELEGRAM ACCOUNT LINKING
        // -----------------------------------------------------

        if (message.Contact != null)
        {
            var linked =
                await _telegram.LinkTelegramAccountAsync(
                    chatId,
                    message.Contact.PhoneNumber);

            if (!linked)
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ لم يتم العثور على حساب REMS مرتبط بهذا الرقم.\n\n" +
                    "تأكد أن رقم الهاتف في REMS مطابق لرقم هاتفك في Telegram.");

                return;
            }

            await _telegram.SendMessageAsync(
                chatId,
                "✅ تم ربط Telegram بحساب REMS بنجاح.");

            await SendMainMenuAsync(
                chatId);

            return;
        }

        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId);

        // -----------------------------------------------------
        // START
        // -----------------------------------------------------

        if (string.Equals(
                message.Text,
                "/start",
                StringComparison.OrdinalIgnoreCase))
        {
            if (user == null)
            {
                await SendLinkMenuAsync(
                    chatId);
            }
            else
            {
                await SendMainMenuAsync(
                    chatId);
            }

            return;
        }

        // -----------------------------------------------------
        // ACCOUNT NOT LINKED
        // -----------------------------------------------------

        if (user == null)
        {
            await SendLinkMenuAsync(
                chatId);

            return;
        }

        // -----------------------------------------------------
        // FILE UPLOAD
        // -----------------------------------------------------

        if (IsFileMessage(message))
        {
            await HandleFileUploadAsync(
                user,
                message);

            return;
        }

        var text =
            message.Text?.Trim();

        // -----------------------------------------------------
        // COMMAND
        // -----------------------------------------------------

        if (!string.IsNullOrWhiteSpace(text) &&
            text.StartsWith("/"))
        {
            await HandleCommandAsync(
                user,
                text);

            return;
        }

        // -----------------------------------------------------
        // STATE MACHINE
        // -----------------------------------------------------

        if (_states.TryGetValue(
                chatId,
                out var state))
        {
            await ContinueStateAsync(
                user,
                state,
                text);

            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            "اختر عملية من القائمة:",
            BuildMainMenu(user));
    }

    // =========================================================
    // FILE MESSAGE DETECTION
    // =========================================================

    private static bool IsFileMessage(
        Telegram.Bot.Types.Message message)
    {
        return
            message.Document != null ||
            (message.Photo != null &&
             message.Photo.Length > 0) ||
            message.Video != null ||
            message.Audio != null ||
            message.Animation != null;
    }

    // =========================================================
    // FILE UPLOAD
    // =========================================================

    private async Task HandleFileUploadAsync(
        User user,
        Telegram.Bot.Types.Message message)
    {
        var chatId =
            user.ChatId!.Value;

        try
        {
            var fileName =
                GetIncomingFileName(
                    message);

            await _telegram.SendMessageAsync(
                chatId,
                $"⏳ جارٍ رفع الملف إلى REMS...\n\n" +
                $"📄 {fileName}\n" +
                "يرجى الانتظار حتى يكتمل الرفع.");

            var storedFile =
                await _telegram.SaveTelegramFileAsync(
                    chatId,
                    message,
                    folderId: null,
                    isSharedHub: false);

            if (storedFile == null)
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ تعذر حفظ الملف.\n\n" +
                    "تأكد أن حساب Telegram مربوط بحساب REMS.");

                return;
            }

            var sizeText =
                FormatFileSize(
                    storedFile.Size);

            await _telegram.SendMessageAsync(
                chatId,
                "✅ تم رفع الملف بنجاح إلى REMS.\n\n" +
                $"📄 الاسم: {storedFile.OriginalName}\n" +
                $"📦 الحجم: {sizeText}\n" +
                $"🆔 File ID: {storedFile.Id}\n\n" +
                "📁 تم حفظ الملف في ملفاتك داخل REMS.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Telegram file upload rejected for user {UserId}",
                user.Id);

            await _telegram.SendMessageAsync(
                chatId,
                $"❌ لم يتم رفع الملف.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram file upload failed for user {UserId}",
                user.Id);

            await _telegram.SendMessageAsync(
                chatId,
                "❌ حدث خطأ أثناء رفع الملف إلى REMS.\n\n" +
                "حاول مرة أخرى لاحقًا.");
        }
    }

    // =========================================================
    // INCOMING FILE NAME
    // =========================================================

    private static string GetIncomingFileName(
        Telegram.Bot.Types.Message message)
    {
        if (message.Document != null)
        {
            return Path.GetFileName(
                string.IsNullOrWhiteSpace(
                    message.Document.FileName)
                    ? "telegram-file"
                    : message.Document.FileName);
        }

        if (message.Photo is { Length: > 0 })
        {
            return
                $"photo-{DateTime.Now:yyyyMMdd-HHmmss}.jpg";
        }

        if (message.Video != null)
        {
            return Path.GetFileName(
                string.IsNullOrWhiteSpace(
                    message.Video.FileName)
                    ? $"video-{DateTime.Now:yyyyMMdd-HHmmss}.mp4"
                    : message.Video.FileName);
        }

        if (message.Audio != null)
        {
            return Path.GetFileName(
                string.IsNullOrWhiteSpace(
                    message.Audio.FileName)
                    ? $"audio-{DateTime.Now:yyyyMMdd-HHmmss}.mp3"
                    : message.Audio.FileName);
        }

        if (message.Animation != null)
        {
            return Path.GetFileName(
                string.IsNullOrWhiteSpace(
                    message.Animation.FileName)
                    ? $"animation-{DateTime.Now:yyyyMMdd-HHmmss}.mp4"
                    : message.Animation.FileName);
        }

        return
            $"telegram-file-{DateTime.Now:yyyyMMdd-HHmmss}";
    }

    // =========================================================
    // FILE SIZE
    // =========================================================

    private static string FormatFileSize(
        long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.##} KB";

        if (bytes < 1024L * 1024L * 1024L)
            return $"{bytes / (1024d * 1024d):0.##} MB";

        return
            $"{bytes / (1024d * 1024d * 1024d):0.##} GB";
    }

    // =========================================================
    // COMMANDS
    // =========================================================

    private async Task HandleCommandAsync(
        User user,
        string text)
    {
        var chatId =
            user.ChatId!.Value;

        var command =
            text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.ToLowerInvariant();

        switch (command)
        {
            case "/start":
            case "/menu":
                await SendMainMenuAsync(
                    chatId);
                break;

            case "/tasks":
                await SendMyTasksAsync(
                    user);
                break;

            case "/today":
                await SendTodayTasksAsync(user);
                break;

            case "/help":
                await SendHelpAsync(user.ChatId!.Value);
                break;

            case "/add":
                if (!settings.AllowTaskCreation) { await SendAccessDeniedAsync(user); break; }
                await StartAddTaskAsync(
                    user);
                break;

            case "/finish":
                await SendTaskSelectionAsync(
                    user,
                    "finish");
                break;

            case "/edit":
                if (!settings.AllowTaskEditing) { await SendAccessDeniedAsync(user); break; }
                await SendTaskSelectionAsync(
                    user,
                    "edit");
                break;

            case "/delete":
                if (!settings.AllowTaskDeletion) { await SendAccessDeniedAsync(user); break; }
                await SendTaskSelectionAsync(
                    user,
                    "delete");
                break;

            case "/overdue":
                await SendOverdueAsync(
                    user);
                break;

            case "/report":
                if (CanViewReports(user))
                    await SendDailyReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);
                break;

            case "/weekly":
                if (CanViewReports(user))
                    await SendWeeklyReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);
                break;

            case "/backlog":
                if (CanViewReports(user))
                    await SendBacklogReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);
                break;

            case "/employees":
                if (CanManageUsers(user))
                    await SendEmployeesAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);
                break;

            case "/addemployee":
                if (CanManageUsers(user))
                    await StartAddEmployeeAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);
                break;

            default:
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ الأمر غير معروف.\n\n" +
                    "استخدم /menu");
                break;
        }
    }

    private async Task SendTodayTasksAsync(User user)
    {
        var tasks = await _telegram.GetUserTasksForDateAsync(user.Id, DateTime.Today);
        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(user.ChatId!.Value, "✅ لا توجد مهام مستحقة اليوم.");
            return;
        }

        await _telegram.SendMessageAsync(user.ChatId!.Value, $"📅 مهام اليوم: {tasks.Count}");
        foreach (var task in tasks)
            await SendTaskCardAsync(user, task);
    }

    private Task SendHelpAsync(long chatId) => _telegram.SendMessageAsync(
        chatId,
        "🆘 أوامر REMS المتاحة:\n\n" +
        "/menu - القائمة الرئيسية\n" +
        "/tasks - مهامي المفتوحة\n" +
        "/today - مهام اليوم\n" +
        "/overdue - المهام المتأخرة\n" +
        "/add - إضافة مهمة\n" +
        "/finish - إنهاء مهمة\n" +
        "/edit - تعديل مهمة\n" +
        "/report - التقرير اليومي\n" +
        "/weekly - التقرير الأسبوعي\n" +
        "/help - عرض المساعدة");

    // =========================================================
    // MAIN MENU
    // =========================================================

    private async Task SendMainMenuAsync(
        long chatId)
    {
        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId);

        if (user == null)
        {
            await SendLinkMenuAsync(
                chatId);

            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            $"👋 أهلاً {user.FullName}\n\n" +
            "REMS Telegram Control Center\n\n" +
            "📎 يمكنك إرسال ملف مباشرة إلى البوت " +
            "ليتم حفظه في File Manager الخاص بك.",
            BuildMainMenu(user));
    }

    private static Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup
        BuildMainMenu(User user)
    {
        var rows =
            new List<
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

        // =====================================================
        // BASIC
        // =====================================================

        rows.Add(new[]
        {
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "📋 مهامي",
                    "tasks"),

            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "➕ إضافة مهمة",
                    "add")
        });

        rows.Add(new[]
        {
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "✅ إنهاء",
                    "finish"),

            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "✏️ تعديل",
                    "edit")
        });

        rows.Add(new[]
        {
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "🗑 حذف",
                    "delete"),

            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "🔴 المتأخرة",
                    "overdue")
        });

        // =====================================================
        // ADMIN
        // =====================================================

        if (CanViewReports(user))
        {
            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "📊 اليوم",
                        "report_daily"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "📊 الأسبوع",
                        "report_weekly")
            });

            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "⚠️ المتراكمة",
                        "backlog")
            });
        }

        // =====================================================
        // USER MANAGEMENT
        // =====================================================

        if (CanManageUsers(user))
        {
            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "👥 الموظفون",
                        "employees"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "👤 إضافة موظف",
                        "add_employee")
            });
        }

        // =====================================================
        // BROADCAST
        // =====================================================

        if (CanBroadcast(user))
        {
            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "📢 إرسال للجميع",
                        "broadcast")
            });
        }

        // =====================================================
        // REFRESH
        // =====================================================

        rows.Add(new[]
        {
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "🔄 تحديث",
                    "refresh")
        });

        return
            new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                rows);
    }

    // =========================================================
    // LINK ACCOUNT
    // =========================================================

    private async Task SendLinkMenuAsync(
        long chatId)
    {
        var keyboard =
            new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(
                new[]
                {
                    new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[]
                    {
                        Telegram.Bot.Types.ReplyMarkups.KeyboardButton
                            .WithRequestContact(
                                "📱 ربط حساب REMS")
                    }
                })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

        await _telegram.Client.SendTextMessageAsync(
            chatId,
            "🔐 هذا الحساب غير مربوط بـ REMS.\n\n" +
            "اضغط على الزر وأرسل رقم هاتفك.",
            replyMarkup: keyboard);
    }

    // =========================================================
    // TASK LIST
    // =========================================================

    private async Task SendMyTasksAsync(
        User user)
    {
        var tasks =
            await _telegram.GetUserTasksAsync(
                user.Id,
                false);

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "✅ لا توجد مهام مفتوحة.");

            return;
        }

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            $"📋 مهامك المفتوحة: {tasks.Count}");

        foreach (var task in tasks)
        {
            await SendTaskCardAsync(
                user,
                task);
        }
    }

    private async Task SendTaskCardAsync(
        User user,
        FollowUpReport task)
    {
        var message =
            $"📌 المهمة #{task.Id}\n\n" +
            $"📝 {task.Content}\n" +
            $"📍 الحالة: {task.IsDoneOrNot ?? "لم تبدأ"}\n" +
            $"📅 الموعد: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد")}\n" +
            $"⭐ الأولوية: {task.Priority}\n" +
            $"📈 التقدم: {task.ProgressPercentage}%";

        var rows =
            new List<
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

        if (task.IsDone)
        {
            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "🔄 إعادة فتح",
                        $"reopen:{task.Id}")
            });
        }
        else
        {
            rows.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "✅ إنهاء",
                        $"finish_task:{task.Id}"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "✏️ تعديل",
                        $"edit_task:{task.Id}")
            });
        }

        rows.Add(new[]
        {
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                .WithCallbackData(
                    "🗑 حذف",
                    $"delete_task:{task.Id}")
        });

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            message,
            new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                rows));
    }

    // =========================================================
    // ADD TASK
    // =========================================================

    private async Task StartAddTaskAsync(
        User user)
    {
        _states[user.ChatId!.Value] =
            new BotState
            {
                Action =
                    BotAction.AddTask,

                Step =
                    1
            };

        await _telegram.SendMessageAsync(
            user.ChatId.Value,
            "➕ إنشاء مهمة جديدة\n\n" +
            "📝 أرسل وصف المهمة:");
    }

    // =========================================================
    // ADD EMPLOYEE
    // =========================================================

    private async Task StartAddEmployeeAsync(
        User admin)
    {
        if (!CanManageUsers(admin))
        {
            await SendAccessDeniedAsync(
                admin);

            return;
        }

        _states[admin.ChatId!.Value] =
            new BotState
            {
                Action =
                    BotAction.AddEmployee,

                Step =
                    1
            };

        await _telegram.SendMessageAsync(
            admin.ChatId.Value,
            GetEmployeeCreationIntro(admin));
    }

    private static string GetEmployeeCreationIntro(
        User admin)
    {
        var department =
            GetAdminDepartment(admin);

        return
            "👤 إضافة حساب جديد\n\n" +
            $"🏢 القسم: {department}\n\n" +
            "اختر نوع الحساب الذي تريد إنشاءه:\n\n" +
            "1️⃣ موظف تابع لقسمك\n" +
            "2️⃣ Admin من نفس قسمك\n\n" +
            "أرسل 1 أو 2";
    }

    // =========================================================
    // STATE MACHINE
    // =========================================================

    private async Task ContinueStateAsync(
        User user,
        BotState state,
        string? text)
    {
        var chatId =
            user.ChatId!.Value;

        // =====================================================
        // ADD TASK
        // =====================================================

        if (state.Action == BotAction.AddTask)
        {
            // -------------------------------------------------
            // STEP 1 = TASK DESCRIPTION
            // -------------------------------------------------

            if (state.Step == 1)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "❌ يجب إرسال وصف المهمة.");

                    return;
                }

                state.Content =
                    text.Trim();

                state.Step =
                    2;

                await SendTaskDueDateCalendarAsync(
                    chatId);

                return;
            }

            // -------------------------------------------------
            // STEP 2 = WAITING FOR CALENDAR SELECTION
            // -------------------------------------------------

            if (state.Step == 2)
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "⚠️ اختيار تاريخ التسليم إلزامي.\n\n" +
                    "استخدم التقويم أعلاه لاختيار التاريخ.");

                return;
            }

            return;
        }

        // =====================================================
        // EDIT
        // =====================================================

        if (state.Action == BotAction.EditTask)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ أرسل النص الجديد للمهمة.");

                return;
            }

            if (!state.TaskId.HasValue)
            {
                _states.TryRemove(
                    chatId,
                    out _);

                return;
            }

            var success =
                await _telegram.UpdateTaskContentAsync(
                    state.TaskId.Value,
                    user.Id,
                    text);

            _states.TryRemove(
                chatId,
                out _);

            await _telegram.SendMessageAsync(
                chatId,
                success
                    ? "✅ تم تعديل المهمة."
                    : "❌ لا يمكنك تعديل هذه المهمة.");

            return;
        }

        // =====================================================
        // ADD EMPLOYEE
        // =====================================================

        if (state.Action == BotAction.AddEmployee)
        {
            if (!CanManageUsers(user))
            {
                _states.TryRemove(
                    chatId,
                    out _);

                await SendAccessDeniedAsync(
                    user);

                return;
            }

            // -------------------------------------------------
            // STEP 1 = ROLE
            // -------------------------------------------------

            if (state.Step == 1)
            {
                if (text == "1")
                {
                    state.NewEmployeeType =
                        EmployeeCreationType
                            .DepartmentEmployee;

                    state.Step =
                        2;

                    await _telegram.SendMessageAsync(
                        chatId,
                        "👤 اخترت: موظف تابع لقسمك.\n\n" +
                        "أرسل الاسم الكامل:");
                }
                else if (text == "2")
                {
                    state.NewEmployeeType =
                        EmployeeCreationType
                            .SameTypeAdmin;

                    state.Step =
                        2;

                    await _telegram.SendMessageAsync(
                        chatId,
                        "👑 اخترت: Admin من نفس نوع قسمك.\n\n" +
                        "أرسل الاسم الكامل:");
                }
                else
                {
                    await SendEmployeeRoleMenu(
                        user);
                }

                return;
            }

            // -------------------------------------------------
            // STEP 2 = NAME
            // -------------------------------------------------

            if (state.Step == 2)
            {
                state.EmployeeFullName =
                    text;

                state.Step =
                    3;

                await _telegram.SendMessageAsync(
                    chatId,
                    "📱 أرسل رقم هاتف الموظف:");

                return;
            }

            // -------------------------------------------------
            // STEP 3 = PHONE
            // -------------------------------------------------

            if (state.Step == 3)
            {
                state.EmployeePhone =
                    text;

                state.Step =
                    4;

                await _telegram.SendMessageAsync(
                    chatId,
                    "📧 أرسل البريد الإلكتروني:");

                return;
            }

            // -------------------------------------------------
            // STEP 4 = EMAIL
            // -------------------------------------------------

            if (state.Step == 4)
            {
                state.EmployeeEmail =
                    text;

                state.Step =
                    5;

                await _telegram.SendMessageAsync(
                    chatId,
                    "🔐 أرسل كلمة المرور:");

                return;
            }

            // -------------------------------------------------
            // STEP 5 = PASSWORD
            // -------------------------------------------------

            if (state.Step == 5)
            {
                var result =
                    await _telegram.CreateEmployeeAsync(
                        state.EmployeeFullName!,
                        state.EmployeePhone!,
                        state.EmployeeEmail!,
                        text!,
                        GetNewEmployeeRole(
                            user,
                            state));

                _states.TryRemove(
                    chatId,
                    out _);

                if (!result.Success)
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "❌ لم يتم إنشاء الحساب.\n\n" +
                        result.Message);

                    return;
                }

                var employee =
                    result.User!;

                await _telegram.SendMessageAsync(
                    chatId,
                    BuildEmployeeCreatedMessage(
                        employee,
                        state));

                return;
            }

            return;
        }

        // =====================================================
        // BROADCAST
        // =====================================================

        if (state.Action == BotAction.Broadcast)
        {
            if (!CanBroadcast(user))
            {
                _states.TryRemove(
                    chatId,
                    out _);

                await SendAccessDeniedAsync(
                    user);

                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ لا يمكن إرسال رسالة فارغة.");

                return;
            }

            await _telegram.SendMessagesToAllUsers(
                text);

            _states.TryRemove(
                chatId,
                out _);

            await _telegram.SendMessageAsync(
                chatId,
                "✅ تم إرسال الرسالة لجميع المستخدمين.");

            return;
        }
    }

    // =========================================================
    // TASK DUE DATE CALENDAR
    // =========================================================

    private async Task SendTaskDueDateCalendarAsync(
        long chatId,
        int monthOffset = 0)
    {
        await _telegram.SendMessageAsync(
            chatId,
            "📅 اختر تاريخ التسليم:\n\n" +
            "⚠️ اختيار الموعد إلزامي.",
            new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                BuildTaskDueDateCalendar(
                    monthOffset)));
    }

    private static List<
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>
        BuildTaskDueDateCalendar(
            int monthOffset)
    {
        var targetDate =
            DateTime.Today.AddMonths(
                monthOffset);

        var year =
            targetDate.Year;

        var month =
            targetDate.Month;

        var firstDay =
            new DateTime(
                year,
                month,
                1);

        var daysInMonth =
            DateTime.DaysInMonth(
                year,
                month);

        var buttons =
            new List<
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

        // =====================================================
        // HEADER
        // =====================================================

        buttons.Add(
            new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        $"📅 {GetArabicMonthName(month)} {year}",
                        "noop")
            });

        // =====================================================
        // WEEK DAYS
        // =====================================================

        buttons.Add(
            new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "أحد",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "اثن",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "ثلا",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "أرب",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "خمي",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "جمع",
                        "noop"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "سبت",
                        "noop")
            });

        // =====================================================
        // EMPTY CELLS
        // =====================================================

        var startDay =
            (int)firstDay.DayOfWeek;

        var row =
            new List<
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton>();

        for (var i = 0;
             i < startDay;
             i++)
        {
            row.Add(
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "·",
                        "noop"));
        }

        // =====================================================
        // DAYS
        // =====================================================

        for (var day = 1;
             day <= daysInMonth;
             day++)
        {
            var date =
                new DateTime(
                    year,
                    month,
                    day);

            // Do not allow selecting dates before today.
            if (date.Date < DateTime.Today)
            {
                row.Add(
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                        .WithCallbackData(
                            "·",
                            "noop"));
            }
            else
            {
                var label =
                    date.Date == DateTime.Today
                        ? $"🟢 {day}"
                        : day.ToString();

                row.Add(
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                        .WithCallbackData(
                            label,
                            $"task_due:{date:yyyy-MM-dd}"));
            }

            if (row.Count == 7)
            {
                buttons.Add(
                    row.ToArray());

                row =
                    new List<
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton>();
            }
        }

        // =====================================================
        // FINAL ROW
        // =====================================================

        if (row.Count > 0)
        {
            while (row.Count < 7)
            {
                row.Add(
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                        .WithCallbackData(
                            "·",
                            "noop"));
            }

            buttons.Add(
                row.ToArray());
        }

        // =====================================================
        // NAVIGATION
        // =====================================================

        buttons.Add(
            new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "◀️ السابق",
                        $"task_due_month:{monthOffset - 1}"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "📅 الحالي",
                        "task_due_month:0"),

                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                    .WithCallbackData(
                        "التالي ▶️",
                        $"task_due_month:{monthOffset + 1}")
            });

        return buttons;
    }

    // =========================================================
    // EMPLOYEE ROLE MENU
    // =========================================================

    private async Task SendEmployeeRoleMenu(
        User admin)
    {
        var department =
            GetAdminDepartment(admin);

        var message =
            "👤 إضافة حساب جديد\n\n" +
            $"🏢 القسم: {department}\n\n" +
            "اختر نوع الحساب:\n\n" +
            "1️⃣ موظف تابع لقسمك\n" +
            "2️⃣ Admin من نفس قسمك\n\n" +
            "أرسل 1 أو 2";

        await _telegram.SendMessageAsync(
            admin.ChatId!.Value,
            message);
    }

    private static string GetAdminDepartment(
        User user)
    {
        if (user.IsItAdmin)
            return "IT";

        if (user.IsFollowUpAdmin)
            return "Follow-Up";

        if (user.IsAdmin)
            return "الإدارة العامة";

        return "غير معروف";
    }

    // =========================================================
    // NEW EMPLOYEE ROLE
    // =========================================================

    private static NewEmployeeRole GetNewEmployeeRole(
        User creator,
        BotState state)
    {
        var isAdmin =
            state.NewEmployeeType ==
            EmployeeCreationType.SameTypeAdmin;

        if (creator.IsItAdmin)
        {
            return new NewEmployeeRole
            {
                IsAdmin =
                    false,

                IsItAdmin =
                    isAdmin,

                IsFollowUpAdmin =
                    false,

                IsFUser =
                    false
            };
        }

        if (creator.IsFollowUpAdmin)
        {
            return new NewEmployeeRole
            {
                IsAdmin =
                    false,

                IsItAdmin =
                    false,

                IsFollowUpAdmin =
                    isAdmin,

                IsFUser =
                    !isAdmin
            };
        }

        if (creator.IsAdmin)
        {
            return new NewEmployeeRole
            {
                IsAdmin =
                    isAdmin,

                IsItAdmin =
                    false,

                IsFollowUpAdmin =
                    false,

                IsFUser =
                    false
            };
        }

        return new NewEmployeeRole
        {
            IsAdmin =
                false,

            IsItAdmin =
                false,

            IsFollowUpAdmin =
                false,

            IsFUser =
                false
        };
    }

    // =========================================================
    // CALLBACKS
    // =========================================================

    private async Task HandleCallbackAsync(
        Telegram.Bot.Types.CallbackQuery callback)
    {
        var chatId =
            callback.Message?.Chat.Id;

        if (!chatId.HasValue)
            return;

        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId.Value);

        if (user == null)
            return;

        await _telegram.Client.AnswerCallbackQueryAsync(
            callback.Id);

        var data =
            callback.Data ??
            string.Empty;

        // =====================================================
        // CALENDAR - NOOP
        // =====================================================

        if (data == "noop")
        {
            return;
        }

        // =====================================================
        // CALENDAR - CHANGE MONTH
        // =====================================================

        if (data.StartsWith(
                "task_due_month:"))
        {
            if (!int.TryParse(
                    data["task_due_month:".Length..],
                    out var monthOffset))
            {
                return;
            }

            if (!_states.TryGetValue(
                    chatId.Value,
                    out var calendarState))
            {
                return;
            }

            if (calendarState.Action !=
                BotAction.AddTask ||
                calendarState.Step != 2)
            {
                return;
            }

            await SendTaskDueDateCalendarAsync(
                chatId.Value,
                monthOffset);

            return;
        }

        // =====================================================
        // CALENDAR - SELECT DATE
        // =====================================================

        // =====================================================
        // CALENDAR - SELECT DATE
        // =====================================================

        if (data.StartsWith(
                "task_due:"))
        {
            var dateText =
                data["task_due:".Length..];

            DateTime selectedDate;

            if (!DateTime.TryParse(
                    dateText,
                    out selectedDate))
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ تعذر قراءة التاريخ المحدد.");

                return;
            }

            if (!_states.TryGetValue(
                    chatId.Value,
                    out var taskState))
            {
                return;
            }

            if (taskState.Action != BotAction.AddTask ||
                taskState.Step != 2)
            {
                return;
            }

            // -------------------------------------------------
            // DATE CANNOT BE IN THE PAST
            // -------------------------------------------------

            if (selectedDate.Date < DateTime.Today)
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ لا يمكنك اختيار تاريخ في الماضي.");

                return;
            }

            // -------------------------------------------------
            // SAVE DATE
            // -------------------------------------------------

            taskState.DueDate =
                selectedDate.Date;

            // -------------------------------------------------
            // ADMIN → SELECT EMPLOYEE
            // -------------------------------------------------

            if (IsAnyAdmin(user))
            {
                taskState.Step = 3;

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    $"✅ تم اختيار موعد التسليم:\n\n" +
                    $"📅 {selectedDate:yyyy-MM-dd}");

                await SendEmployeeSelectionForCreationAsync(
                    chatId.Value);

                return;
            }

            // -------------------------------------------------
            // NORMAL USER → CREATE TASK
            // -------------------------------------------------

            var task =
                await _telegram.CreateTaskAsync(
                    user.Id,
                    user.Id,
                    taskState.Content!,
                    taskState.DueDate,
                    0);

            _states.TryRemove(
                chatId.Value,
                out _);

            await _telegram.SendMessageAsync(
                chatId.Value,
                task == null
                    ? "❌ تعذر إنشاء المهمة."
                    : $"✅ تم إنشاء المهمة #{task.Id}.\n\n" +
                      $"📅 موعد التسليم: {taskState.DueDate:yyyy-MM-dd}");

            return;
        }

        // =====================================================
        // NORMAL CALLBACKS
        // =====================================================

        switch (data)
        {
            case "tasks":

                await SendMyTasksAsync(
                    user);

                return;

            case "add":

                await StartAddTaskAsync(
                    user);

                return;

            case "finish":

                await SendTaskSelectionAsync(
                    user,
                    "finish");

                return;

            case "edit":

                await SendTaskSelectionAsync(
                    user,
                    "edit");

                return;

            case "delete":

                await SendTaskSelectionAsync(
                    user,
                    "delete");

                return;

            case "overdue":

                await SendOverdueAsync(
                    user);

                return;

            case "refresh":

                await SendMainMenuAsync(
                    chatId.Value);

                return;

            case "report_daily":

                if (CanViewReports(user))
                    await SendDailyReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);

                return;

            case "report_weekly":

                if (CanViewReports(user))
                    await SendWeeklyReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);

                return;

            case "backlog":

                if (CanViewReports(user))
                    await SendBacklogReportAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);

                return;

            case "employees":

                if (CanManageUsers(user))
                    await SendEmployeesAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);

                return;

            case "add_employee":

                if (CanManageUsers(user))
                    await StartAddEmployeeAsync(
                        user);
                else
                    await SendAccessDeniedAsync(
                        user);

                return;

            case "broadcast":

                if (CanBroadcast(user))
                {
                    _states[user.ChatId!.Value] =
                        new BotState
                        {
                            Action =
                                BotAction.Broadcast,

                            Step =
                                1
                        };

                    await _telegram.SendMessageAsync(
                        user.ChatId.Value,
                        "📢 أرسل الرسالة التي تريد إرسالها لجميع المستخدمين.");
                }
                else
                {
                    await SendAccessDeniedAsync(
                        user);
                }

                return;
        }

        // =====================================================
        // TASK ACTIONS
        // =====================================================

        if (data.StartsWith(
                "finish_task:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.CompleteTaskAsync(
                        id.Value,
                        user.Id);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"✅ تم إنهاء المهمة #{id.Value}."
                        : "❌ لا يمكنك إنهاء هذه المهمة.");
            }

            return;
        }

        if (data.StartsWith(
                "reopen:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.ReopenTaskAsync(
                        id.Value,
                        user.Id);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"🔄 تمت إعادة فتح المهمة #{id.Value}."
                        : "❌ تعذر إعادة فتح المهمة.");
            }

            return;
        }

        if (data.StartsWith(
                "delete_task:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.DeleteTaskAsync(
                        id.Value,
                        user.Id);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"🗑 تم حذف المهمة #{id.Value}."
                        : "❌ لا يمكنك حذف هذه المهمة.");
            }

            return;
        }

        if (data.StartsWith(
                "edit_task:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                _states[chatId.Value] =
                    new BotState
                    {
                        Action =
                            BotAction.EditTask,

                        TaskId =
                            id.Value,

                        Step =
                            1
                    };

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    $"✏️ تعديل المهمة #{id.Value}\n\n" +
                    "أرسل النص الجديد:");
            }

            return;
        }

        // =====================================================
        // ASSIGN TASK
        // =====================================================

        if (data.StartsWith(
                "assign_employee:"))
        {
            if (!CanManageUsers(user))
                return;

            var employeeId =
                ParseId(data);

            if (!employeeId.HasValue)
                return;

            if (!_states.TryGetValue(
                    chatId.Value,
                    out var state))
                return;

            if (state.Action !=
                BotAction.AddTask)
                return;

            if (!state.DueDate.HasValue)
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ يجب اختيار تاريخ التسليم أولًا.");

                await SendTaskDueDateCalendarAsync(
                    chatId.Value);

                return;
            }

            var employee =
                await _telegram.GetUserByIdAsync(
                    employeeId.Value);

            if (employee == null)
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ الموظف غير موجود.");

                return;
            }

            var task =
                await _telegram.CreateTaskAsync(
                    user.Id,
                    employeeId.Value,
                    state.Content!,
                    state.DueDate,
                    0);

            _states.TryRemove(
                chatId.Value,
                out _);

            if (task == null)
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ تعذر إنشاء المهمة.");

                return;
            }

            await _telegram.SendMessageAsync(
                chatId.Value,
                $"✅ تم إنشاء المهمة #{task.Id}\n\n" +
                $"👤 الموظف: {employee.FullName}\n" +
                $"📅 موعد التسليم: {task.DueDate:yyyy-MM-dd}");

            if (employee.ChatId.HasValue)
            {
                await _telegram.SendMessageAsync(
                    employee.ChatId.Value,
                    $"🔔 تم إسناد مهمة جديدة لك.\n\n" +
                    $"📌 #{task.Id}\n" +
                    $"📝 {task.Content}\n" +
                    $"📅 موعد التسليم: {task.DueDate:yyyy-MM-dd}");
            }

            return;
        }
    }

    // =========================================================
    // TASK SELECTION
    // =========================================================

    private async Task SendTaskSelectionAsync(
        User user,
        string action)
    {
        var tasks =
            await _telegram.GetUserTasksAsync(
                user.Id,
                true);

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "لا توجد مهام.");

            return;
        }

        var buttons =
            tasks
                .Take(30)
                .Select(
                    task =>
                        new[]
                        {
                            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                                .WithCallbackData(
                                    $"#{task.Id} {Shorten(task.Content, 30)}",
                                    action == "finish"
                                        ? $"finish_task:{task.Id}"
                                        : action == "edit"
                                            ? $"edit_task:{task.Id}"
                                            : $"delete_task:{task.Id}")
                        })
                .ToList();

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            action switch
            {
                "finish" =>
                    "✅ اختر المهمة التي تريد إنهاءها:",

                "edit" =>
                    "✏️ اختر المهمة التي تريد تعديلها:",

                "delete" =>
                    "🗑 اختر المهمة التي تريد حذفها:",

                _ =>
                    "اختر المهمة:"
            },
            new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                buttons));
    }

    // =========================================================
    // EMPLOYEE SELECTION FOR TASK
    // =========================================================

    private async Task SendEmployeeSelectionForCreationAsync(
        long chatId)
    {
        var admin =
            await _telegram.GetUserByChatIdAsync(
                chatId);

        if (admin == null)
            return;

        var employees =
            await GetVisibleEmployeesAsync(
                admin);

        if (employees.Count == 0)
        {
            await _telegram.SendMessageAsync(
                chatId,
                "❌ لا يوجد موظفون يمكنك إسناد المهمة إليهم.");

            return;
        }

        var buttons =
            employees
                .Take(50)
                .Select(
                    employee =>
                        new[]
                        {
                            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton
                                .WithCallbackData(
                                    $"👤 {employee.FullName ?? $"User #{employee.Id}"}",
                                    $"assign_employee:{employee.Id}")
                        })
                .ToList();

        await _telegram.SendMessageAsync(
            chatId,
            $"👤 اختر الموظف المسؤول ({GetAdminDepartment(admin)}):",
            new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                buttons));
    }

    // =========================================================
    // VISIBLE EMPLOYEES
    // =========================================================

    private async Task<List<User>> GetVisibleEmployeesAsync(
        User admin)
    {
        var all =
            await _telegram.GetEmployeesAsync();

        if (admin.IsAdmin)
        {
            return all
                .Where(
                    x => x.Id != admin.Id)
                .ToList();
        }

        if (admin.IsItAdmin)
        {
            return all
                .Where(
                    x =>
                        x.Id != admin.Id &&
                        x.IsItAdmin)
                .ToList();
        }

        if (admin.IsFollowUpAdmin)
        {
            return all
                .Where(
                    x =>
                        x.Id != admin.Id &&
                        (x.IsFollowUpAdmin ||
                         x.IsFUser))
                .ToList();
        }

        return new List<User>();
    }

    // =========================================================
    // OVERDUE
    // =========================================================

    private async Task SendOverdueAsync(
        User user)
    {
        var tasks =
            await _telegram.GetOverdueTasksAsync();

        if (!user.IsAdmin)
        {
            var visibleEmployees =
                await GetVisibleEmployeesAsync(
                    user);

            var visibleIds =
                visibleEmployees
                    .Select(x => x.Id)
                    .ToHashSet();

            visibleIds.Add(
                user.Id);

            tasks =
                tasks
                    .Where(
                        x =>
                            x.AssignedEmployeeId.HasValue &&
                            visibleIds.Contains(
                                x.AssignedEmployeeId.Value))
                    .ToList();
        }

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "✅ لا توجد مهام متأخرة.");

            return;
        }

        var text =
            "🔴 المهام المتأخرة\n\n" +
            string.Join(
                "\n\n",
                tasks.Select(
                    x =>
                        $"#{x.Id} {x.Content}\n" +
                        $"📅 {x.DueDate:yyyy-MM-dd}\n" +
                        $"📍 {x.IsDoneOrNot ?? "لم تبدأ"}"));

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            text);
    }

    // =========================================================
    // REPORTS
    // =========================================================

    private async Task SendDailyReportAsync(
        User user)
    {
        var report =
            await _telegram.BuildDailyReportAsync(
                DateTime.Today);

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            report);
    }

    private async Task SendWeeklyReportAsync(
        User user)
    {
        var today =
            DateTime.Today;

        var diff =
            (7 +
             (today.DayOfWeek -
              DayOfWeek.Saturday))
            % 7;

        var start =
            today.AddDays(-diff);

        var report =
            await _telegram.BuildWeeklyReportAsync(
                start);

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            report);
    }

    private async Task SendBacklogReportAsync(
        User user)
    {
        var report =
            await _telegram.BuildBacklogReportAsync();

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            report);
    }

    // =========================================================
    // EMPLOYEES
    // =========================================================

    private async Task SendEmployeesAsync(
        User admin)
    {
        var employees =
            await GetVisibleEmployeesAsync(
                admin);

        if (employees.Count == 0)
        {
            await _telegram.SendMessageAsync(
                admin.ChatId!.Value,
                "لا يوجد مستخدمون ضمن نطاق إدارتك.");

            return;
        }

        var message =
            $"👥 موظفو {GetAdminDepartment(admin)}\n\n" +
            string.Join(
                "\n\n",
                employees.Select(
                    e =>
                        $"{GetRoleIcon(e)} " +
                        $"#{e.Id} {e.FullName}\n" +
                        $"📱 Telegram: {(e.ChatId.HasValue ? "✅" : "❌")}\n" +
                        $"📧 {e.Email}"));

        await _telegram.SendMessageAsync(
            admin.ChatId!.Value,
            message);
    }

    private static string GetRoleIcon(
        User user)
    {
        if (user.IsAdmin)
            return "👑";

        if (user.IsItAdmin)
            return "🛠";

        if (user.IsFollowUpAdmin)
            return "📋";

        return "👤";
    }

    // =========================================================
    // CREATED MESSAGE
    // =========================================================

    private static string BuildEmployeeCreatedMessage(
        User employee,
        BotState state)
    {
        var role =
            employee.IsAdmin
                ? "👑 Admin عام"
                : employee.IsItAdmin
                    ? "🛠 IT Admin"
                    : employee.IsFollowUpAdmin
                        ? "📋 Follow-Up Admin"
                        : employee.IsFUser
                            ? "👤 موظف Follow-Up"
                            : "👤 موظف القسم";

        return
            "✅ تم إنشاء الحساب بنجاح.\n\n" +
            $"🆔 ID: {employee.Id}\n" +
            $"👤 الاسم: {employee.FullName}\n" +
            $"📱 الهاتف: {employee.PhoneNumber}\n" +
            $"📧 البريد: {employee.Email}\n" +
            $"🔐 الصلاحية: {role}\n\n" +
            "🔗 الحساب جاهز لتسجيل الدخول إلى REMS.\n" +
            "وعند فتح Telegram لأول مرة يمكن للموظف ربط حسابه.";
    }

    // =========================================================
    // PERMISSIONS
    // =========================================================

    private static bool IsAnyAdmin(
        User user)
    {
        return user.IsAdmin ||
               user.IsItAdmin ||
               user.IsFollowUpAdmin;
    }

    private static bool CanManageUsers(
        User user)
    {
        return user.IsAdmin ||
               user.IsItAdmin ||
               user.IsFollowUpAdmin;
    }

    private static bool CanViewReports(
        User user)
    {
        return user.IsAdmin ||
               user.IsItAdmin ||
               user.IsFollowUpAdmin;
    }

    private static bool CanBroadcast(
        User user)
    {
        return user.IsAdmin;
    }

    // =========================================================
    // ACCESS DENIED
    // =========================================================

    private async Task SendAccessDeniedAsync(
        User user)
    {
        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            "⛔ ليس لديك صلاحية لتنفيذ هذه العملية.");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static int? ParseId(
        string text)
    {
        var parts =
            text.Split(
                ':',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return null;

        return int.TryParse(
            parts[1],
            out var id)
            ? id
            : null;
    }

    private static string Shorten(
        string? text,
        int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "بدون وصف";

        return text.Length <= max
            ? text
            : text[..max] + "...";
    }

    // =========================================================
    // STATE
    // =========================================================

    private sealed class BotState
    {
        public BotAction Action { get; set; }

        public int Step { get; set; }

        public string? Content { get; set; }

        public DateTime? DueDate { get; set; }

        public int? TaskId { get; set; }

        // -----------------------------
        // New Employee
        // -----------------------------

        public EmployeeCreationType NewEmployeeType { get; set; }

        public string? EmployeeFullName { get; set; }

        public string? EmployeePhone { get; set; }

        public string? EmployeeEmail { get; set; }
    }

    private enum BotAction
    {
        AddTask,
        EditTask,
        Broadcast,
        AddEmployee
    }

    private enum EmployeeCreationType
    {
        DepartmentEmployee,
        SameTypeAdmin
    }
}

// =============================================================
// NEW EMPLOYEE ROLE
// =============================================================
// IMPORTANT:
// This class is outside TelegramBotHostedService so it can be
// shared directly with TelegramService.
// =============================================================

public class NewEmployeeRole
{
    public bool IsAdmin { get; set; }

    public bool IsItAdmin { get; set; }

    public bool IsFollowUpAdmin { get; set; }

    public bool IsFUser { get; set; }
}
