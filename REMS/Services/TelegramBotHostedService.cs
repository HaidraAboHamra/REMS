using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using REMS.Enititys;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace REMS.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly TelegramService _telegram;
    private readonly IDbContextFactory<REMS.Data.AppDbContext> _dbFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;

    private readonly ConcurrentDictionary<long, BotState> _states = new();

    public TelegramBotHostedService(
        TelegramService telegram,
        IDbContextFactory<REMS.Data.AppDbContext> dbFactory,
        ILogger<TelegramBotHostedService> logger)
    {
        _telegram = telegram;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "REMS Telegram Bot is starting...");

        var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
        {
            AllowedUpdates =
            new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
            ThrowPendingUpdates = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _telegram.Client.ReceiveAsync(
                    HandleUpdateAsync,
                    HandleErrorAsync,
                    receiverOptions,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Telegram polling crashed. Restarting...");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "REMS Telegram Bot stopped.");
    }

    private async Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Telegram bot polling error.");
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type == UpdateType.CallbackQuery &&
                update.CallbackQuery != null)
            {
                await HandleCallbackAsync(
                    update.CallbackQuery,
                    cancellationToken);

                return;
            }

            if (update.Type == UpdateType.Message &&
                update.Message != null)
            {
                await HandleMessageAsync(
                    update.Message,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while processing Telegram update.");
        }
    }

    // =========================================================
    // MESSAGE
    // =========================================================

    private async Task HandleMessageAsync(
        Message message,
        CancellationToken cancellationToken)
    {
        if (message.Chat == null)
            return;

        var chatId = message.Chat.Id;

        // -----------------------------------------------------
        // CONTACT LINKING
        // -----------------------------------------------------

        if (message.Contact != null)
        {
            var linked =
                await _telegram.LinkTelegramAccountAsync(
                    chatId,
                    message.Contact.PhoneNumber,
                    cancellationToken);

            if (!linked)
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "❌ لم يتم العثور على حساب REMS مرتبط برقم الهاتف هذا.\n\n" +
                    "تأكد أن رقم الهاتف الموجود في REMS مطابق لرقم Telegram.",
                    cancellationToken: cancellationToken);

                return;
            }

            await _telegram.SendMessageAsync(
                chatId,
                "✅ تم ربط حساب Telegram مع REMS بنجاح.",
                cancellationToken: cancellationToken);

            await SendMainMenuAsync(
                chatId,
                cancellationToken);

            return;
        }

        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId,
                cancellationToken);

        // -----------------------------------------------------
        // START
        // -----------------------------------------------------

        if (message.Text?.Equals(
                "/start",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            if (user == null)
            {
                await SendLinkMenuAsync(
                    chatId,
                    cancellationToken);
            }
            else
            {
                await SendMainMenuAsync(
                    chatId,
                    cancellationToken);
            }

            return;
        }

        // -----------------------------------------------------
        // NOT LINKED
        // -----------------------------------------------------

        if (user == null)
        {
            await SendLinkMenuAsync(
                chatId,
                cancellationToken);

            return;
        }

        // -----------------------------------------------------
        // TEXT COMMANDS
        // -----------------------------------------------------

        var text = message.Text?.Trim();

        if (!string.IsNullOrWhiteSpace(text) &&
            text.StartsWith("/"))
        {
            await HandleCommandAsync(
                user,
                text,
                cancellationToken);

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
                text,
                cancellationToken);

            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            "اختر عملية من القائمة الرئيسية.",
            MainMenuKeyboard(user),
            cancellationToken);
    }

    // =========================================================
    // COMMANDS
    // =========================================================

    private async Task HandleCommandAsync(
        User user,
        string text,
        CancellationToken cancellationToken)
    {
        var chatId = user.ChatId!.Value;

        var command =
            text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.ToLowerInvariant();

        switch (command)
        {
            case "/menu":

                await SendMainMenuAsync(
                    chatId,
                    cancellationToken);

                break;

            case "/tasks":

                await SendMyTasksAsync(
                    user,
                    cancellationToken);

                break;

            case "/overdue":

                await SendOverdueAsync(
                    user,
                    cancellationToken);

                break;

            case "/add":

                await StartAddTaskAsync(
                    user,
                    cancellationToken);

                break;

            case "/finish":

                await SendTaskSelectionAsync(
                    user,
                    "finish",
                    cancellationToken);

                break;

            case "/edit":

                await SendTaskSelectionAsync(
                    user,
                    "edit",
                    cancellationToken);

                break;

            case "/delete":

                await SendTaskSelectionAsync(
                    user,
                    "delete",
                    cancellationToken);

                break;

            case "/report":

                if (!IsAdmin(user))
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "⛔ هذه العملية متاحة للمدير فقط.",
                        cancellationToken: cancellationToken);

                    return;
                }

                var daily =
                    await _telegram.BuildDailyReportAsync(
                        DateTime.Today,
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId,
                    daily,
                    cancellationToken: cancellationToken);

                break;

            case "/weekly":

                if (!IsAdmin(user))
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "⛔ هذه العملية متاحة للمدير فقط.",
                        cancellationToken: cancellationToken);

                    return;
                }

                var today = DateTime.Today;

                var diff =
                    (7 +
                     (today.DayOfWeek - DayOfWeek.Saturday))
                    % 7;

                var weekStart =
                    today.AddDays(-diff);

                var weekly =
                    await _telegram.BuildWeeklyReportAsync(
                        weekStart,
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId,
                    weekly,
                    cancellationToken: cancellationToken);

                break;

            case "/backlog":

                if (!IsAdmin(user))
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "⛔ هذه العملية متاحة للمدير فقط.",
                        cancellationToken: cancellationToken);

                    return;
                }

                var backlog =
                    await _telegram.BuildBacklogReportAsync(
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId,
                    backlog,
                    cancellationToken: cancellationToken);

                break;

            case "/employees":

                if (!IsAdmin(user))
                {
                    await _telegram.SendMessageAsync(
                        chatId,
                        "⛔ هذه العملية متاحة للمدير فقط.",
                        cancellationToken: cancellationToken);

                    return;
                }

                await SendEmployeesAsync(
                    user,
                    cancellationToken);

                break;

            default:

                await _telegram.SendMessageAsync(
                    chatId,
                    "الأمر غير معروف.\nاستخدم /menu",
                    cancellationToken: cancellationToken);

                break;
        }
    }

    // =========================================================
    // MAIN MENU
    // =========================================================

    private async Task SendMainMenuAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId,
                cancellationToken);

        if (user == null)
        {
            await SendLinkMenuAsync(
                chatId,
                cancellationToken);

            return;
        }

        var greeting =
            $"👋 أهلاً {user.FullName}!\n\n" +
            "REMS Telegram Control Center";

        await _telegram.SendMessageAsync(
            chatId,
            greeting,
            MainMenuKeyboard(user),
            cancellationToken);
    }

    private static InlineKeyboardMarkup MainMenuKeyboard(
        User user)
    {
        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "📋 مهامي",
                    "tasks"),

                InlineKeyboardButton.WithCallbackData(
                    "➕ إضافة مهمة",
                    "add")
            },

            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "✅ إنهاء مهمة",
                    "finish"),

                InlineKeyboardButton.WithCallbackData(
                    "✏️ تعديل مهمة",
                    "edit")
            },

            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "🗑 حذف مهمة",
                    "delete"),

                InlineKeyboardButton.WithCallbackData(
                    "⚠️ المتأخرة",
                    "overdue")
            },

            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "🔄 تحديث",
                    "refresh")
            }
        };

        if (IsAdmin(user))
        {
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📊 تقرير اليوم",
                        "report_daily"),

                    InlineKeyboardButton.WithCallbackData(
                        "📊 التقرير الأسبوعي",
                        "report_weekly")
                });

            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "⚠️ المتراكمة",
                        "backlog"),

                    InlineKeyboardButton.WithCallbackData(
                        "👥 الموظفون",
                        "employees")
                });

            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📢 إرسال للجميع",
                        "broadcast")
                });
        }

        return new InlineKeyboardMarkup(rows);
    }

    // =========================================================
    // LINK
    // =========================================================

    private async Task SendLinkMenuAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var keyboard =
            new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        KeyboardButton.WithRequestContact(
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
            "اضغط على الزر وأرسل رقم هاتفك الموجود في حساب REMS.",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    // =========================================================
    // TASK LIST
    // =========================================================

    private async Task SendMyTasksAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var tasks =
            await _telegram.GetUserTasksAsync(
                user.Id,
                false,
                cancellationToken);

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "✅ لا توجد مهام مفتوحة حاليًا.",
                cancellationToken: cancellationToken);

            return;
        }

        foreach (var task in tasks)
        {
            await SendTaskCardAsync(
                user,
                task,
                cancellationToken);
        }
    }

    private async Task SendTaskCardAsync(
        User user,
        FollowUpReport task,
        CancellationToken cancellationToken)
    {
        var text =
            $"📌 المهمة #{task.Id}\n\n" +
            $"📝 {task.Content}\n\n" +
            $"📍 الحالة: {task.IsDoneOrNot ?? "غير محددة"}\n" +
            $"📅 الموعد: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد")}\n" +
            $"⭐ الأولوية: {task.Priority}\n";

        var buttons =
            new List<InlineKeyboardButton[]>();

        if (!task.IsDone)
        {
            buttons.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ إنهاء",
                        $"task_finish:{task.Id}"),

                    InlineKeyboardButton.WithCallbackData(
                        "✏️ تعديل",
                        $"task_edit:{task.Id}")
                });
        }
        else
        {
            buttons.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "🔄 إعادة فتح",
                        $"task_reopen:{task.Id}")
                });
        }

        buttons.Add(
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "🗑 حذف",
                    $"task_delete:{task.Id}")
            });

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            text,
            new InlineKeyboardMarkup(buttons),
            cancellationToken);
    }

    // =========================================================
    // ADD TASK
    // =========================================================

    private async Task StartAddTaskAsync(
        User user,
        CancellationToken cancellationToken)
    {
        _states[user.ChatId!.Value] =
            new BotState
            {
                Action = BotAction.AddTask,
                Step = 1
            };

        await _telegram.SendMessageAsync(
            user.ChatId.Value,
            "➕ إنشاء مهمة جديدة\n\n" +
            "أرسل وصف المهمة:",
            cancellationToken: cancellationToken);
    }

    private async Task ContinueStateAsync(
        User user,
        BotState state,
        string? text,
        CancellationToken cancellationToken)
    {
        var chatId = user.ChatId!.Value;

        if (string.IsNullOrWhiteSpace(text))
        {
            await _telegram.SendMessageAsync(
                chatId,
                "أرسل نصًا صحيحًا.",
                cancellationToken: cancellationToken);

            return;
        }

        // =====================================================
        // ADD TASK
        // =====================================================

        if (state.Action == BotAction.AddTask)
        {
            if (state.Step == 1)
            {
                state.Content = text;
                state.Step = 2;

                await _telegram.SendMessageAsync(
                    chatId,
                    "📅 أرسل تاريخ الاستحقاق بصيغة:\n\n" +
                    "2026-09-20\n\n" +
                    "أو اكتب:\n" +
                    "بدون موعد",
                    cancellationToken: cancellationToken);

                return;
            }

            if (state.Step == 2)
            {
                DateTime? dueDate = null;

                if (!text.Equals(
                        "بدون موعد",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!DateTime.TryParse(text, out var parsed))
                    {
                        await _telegram.SendMessageAsync(
                            chatId,
                            "❌ التاريخ غير صحيح.\nمثال: 2026-09-20",
                            cancellationToken: cancellationToken);

                        return;
                    }

                    dueDate = parsed.Date;
                }

                state.DueDate = dueDate;
                state.Step = 3;

                if (IsAdmin(user))
                {
                    await SendEmployeeSelectionForCreationAsync(
                        chatId,
                        cancellationToken);
                }
                else
                {
                    state.AssignedEmployeeId = user.Id;

                    var task =
                        await _telegram.CreateTaskAsync(
                            user.Id,
                            user.Id,
                            state.Content!,
                            state.DueDate,
                            0,
                            cancellationToken);

                    _states.TryRemove(chatId, out _);

                    if (task == null)
                    {
                        await _telegram.SendMessageAsync(
                            chatId,
                            "❌ تعذر إنشاء المهمة.",
                            cancellationToken: cancellationToken);

                        return;
                    }

                    await _telegram.SendMessageAsync(
                        chatId,
                        $"✅ تم إنشاء المهمة #{task.Id} بنجاح.",
                        cancellationToken: cancellationToken);

                    return;
                }

                return;
            }
        }

        // =====================================================
        // EDIT CONTENT
        // =====================================================

        if (state.Action == BotAction.EditTaskContent)
        {
            if (!state.TaskId.HasValue)
            {
                _states.TryRemove(chatId, out _);
                return;
            }

            var success =
                await _telegram.UpdateTaskContentAsync(
                    state.TaskId.Value,
                    user.Id,
                    text,
                    cancellationToken);

            _states.TryRemove(chatId, out _);

            await _telegram.SendMessageAsync(
                chatId,
                success
                    ? "✅ تم تعديل المهمة بنجاح."
                    : "❌ لا يمكنك تعديل هذه المهمة.",
                cancellationToken: cancellationToken);

            return;
        }

        // =====================================================
        // BROADCAST
        // =====================================================

        if (state.Action == BotAction.Broadcast)
        {
            if (!IsAdmin(user))
            {
                _states.TryRemove(chatId, out _);
                return;
            }

            await _telegram.SendMessagesToAllUsers(
                text,
                cancellationToken);

            _states.TryRemove(chatId, out _);

            await _telegram.SendMessageAsync(
                chatId,
                "✅ تم إرسال الرسالة لجميع المستخدمين المرتبطين.",
                cancellationToken: cancellationToken);

            return;
        }
    }

    // =========================================================
    // CALLBACKS
    // =========================================================

    private async Task HandleCallbackAsync(
        CallbackQuery callback,
        CancellationToken cancellationToken)
    {
        var chatId = callback.Message?.Chat.Id;

        if (!chatId.HasValue)
            return;

        var user =
            await _telegram.GetUserByChatIdAsync(
                chatId.Value,
                cancellationToken);

        if (user == null)
            return;

        await _telegram.Client.AnswerCallbackQueryAsync(
            callback.Id,
            cancellationToken: cancellationToken);

        var data = callback.Data ?? string.Empty;

        // -----------------------------------------------------
        // MAIN MENU
        // -----------------------------------------------------

        switch (data)
        {
            case "tasks":

                await SendMyTasksAsync(
                    user,
                    cancellationToken);

                return;

            case "add":

                await StartAddTaskAsync(
                    user,
                    cancellationToken);

                return;

            case "finish":

                await SendTaskSelectionAsync(
                    user,
                    "finish",
                    cancellationToken);

                return;

            case "edit":

                await SendTaskSelectionAsync(
                    user,
                    "edit",
                    cancellationToken);

                return;

            case "delete":

                await SendTaskSelectionAsync(
                    user,
                    "delete",
                    cancellationToken);

                return;

            case "overdue":

                await SendOverdueAsync(
                    user,
                    cancellationToken);

                return;

            case "refresh":

                await SendMainMenuAsync(
                    chatId.Value,
                    cancellationToken);

                return;

            case "report_daily":

                if (IsAdmin(user))
                {
                    var report =
                        await _telegram.BuildDailyReportAsync(
                            DateTime.Today,
                            cancellationToken);

                    await _telegram.SendMessageAsync(
                        chatId.Value,
                        report,
                        cancellationToken: cancellationToken);
                }

                return;

            case "report_weekly":

                if (IsAdmin(user))
                {
                    var today = DateTime.Today;

                    var diff =
                        (7 +
                         (today.DayOfWeek - DayOfWeek.Saturday))
                        % 7;

                    var weekStart =
                        today.AddDays(-diff);

                    var report =
                        await _telegram.BuildWeeklyReportAsync(
                            weekStart,
                            cancellationToken);

                    await _telegram.SendMessageAsync(
                        chatId.Value,
                        report,
                        cancellationToken: cancellationToken);
                }

                return;

            case "backlog":

                if (IsAdmin(user))
                {
                    var backlog =
                        await _telegram.BuildBacklogReportAsync(
                            cancellationToken);

                    await _telegram.SendMessageAsync(
                        chatId.Value,
                        backlog,
                        cancellationToken: cancellationToken);
                }

                return;

            case "employees":

                if (IsAdmin(user))
                {
                    await SendEmployeesAsync(
                        user,
                        cancellationToken);
                }

                return;

            case "broadcast":

                if (IsAdmin(user))
                {
                    _states[user.ChatId!.Value] =
                        new BotState
                        {
                            Action = BotAction.Broadcast,
                            Step = 1
                        };

                    await _telegram.SendMessageAsync(
                        user.ChatId.Value,
                        "📢 أرسل الرسالة التي تريد إرسالها لجميع المستخدمين:",
                        cancellationToken: cancellationToken);
                }

                return;
        }

        // -----------------------------------------------------
        // TASK CALLBACKS
        // -----------------------------------------------------

        if (data.StartsWith("task_finish:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.CompleteTaskAsync(
                        id.Value,
                        user.Id,
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"✅ تم إنهاء المهمة #{id.Value}."
                        : "❌ لا يمكنك إنهاء هذه المهمة.",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (data.StartsWith("task_reopen:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.ReopenTaskAsync(
                        id.Value,
                        user.Id,
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"🔄 تمت إعادة فتح المهمة #{id.Value}."
                        : "❌ لا يمكنك إعادة فتح هذه المهمة.",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (data.StartsWith("task_delete:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                var success =
                    await _telegram.DeleteTaskAsync(
                        id.Value,
                        user.Id,
                        cancellationToken);

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    success
                        ? $"🗑 تم حذف المهمة #{id.Value}."
                        : "❌ لا يمكنك حذف هذه المهمة.",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (data.StartsWith("task_edit:"))
        {
            var id =
                ParseId(data);

            if (id.HasValue)
            {
                _states[chatId.Value] =
                    new BotState
                    {
                        Action = BotAction.EditTaskContent,
                        TaskId = id.Value,
                        Step = 1
                    };

                await _telegram.SendMessageAsync(
                    chatId.Value,
                    $"✏️ تعديل المهمة #{id.Value}\n\n" +
                    "أرسل النص الجديد للمهمة:",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        // -----------------------------------------------------
        // ADMIN EMPLOYEE ASSIGNMENT
        // -----------------------------------------------------

        if (data.StartsWith("assign_employee:"))
        {
            if (!IsAdmin(user))
                return;

            var employeeId =
                ParseId(data);

            if (!employeeId.HasValue)
                return;

            var state =
                _states.TryGetValue(
                    chatId.Value,
                    out var existing)
                    ? existing
                    : null;

            if (state == null ||
                state.Action != BotAction.AddTask)
                return;

            state.AssignedEmployeeId =
                employeeId.Value;

            state.Step = 4;

            var task =
                await _telegram.CreateTaskAsync(
                    user.Id,
                    employeeId.Value,
                    state.Content!,
                    state.DueDate,
                    0,
                    cancellationToken);

            _states.TryRemove(
                chatId.Value,
                out _);

            if (task == null)
            {
                await _telegram.SendMessageAsync(
                    chatId.Value,
                    "❌ تعذر إنشاء المهمة.",
                    cancellationToken: cancellationToken);

                return;
            }

            await _telegram.SendMessageAsync(
                chatId.Value,
                $"✅ تم إنشاء المهمة #{task.Id} بنجاح.",
                cancellationToken: cancellationToken);

            var employee =
                await _telegram.GetUserByIdAsync(
                    employeeId.Value,
                    cancellationToken);

            if (employee?.ChatId.HasValue == true)
            {
                await _telegram.SendMessageAsync(
                    employee.ChatId.Value,
                    $"🔔 تم إسناد مهمة جديدة لك.\n\n" +
                    $"📌 #{task.Id}\n" +
                    $"📝 {task.Content}\n" +
                    $"📅 الموعد: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد")}",
                    cancellationToken: cancellationToken);
            }

            return;
        }
    }

    // =========================================================
    // TASK SELECTION
    // =========================================================

    private async Task SendTaskSelectionAsync(
        User user,
        string action,
        CancellationToken cancellationToken)
    {
        var tasks =
            await _telegram.GetUserTasksAsync(
                user.Id,
                true,
                cancellationToken);

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "لا توجد مهام.",
                cancellationToken: cancellationToken);

            return;
        }

        var buttons =
            tasks
                .Take(30)
                .Select(task =>
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            $"#{task.Id} {Shorten(task.Content, 35)}",
                            action == "finish"
                                ? $"task_finish:{task.Id}"
                                : action == "edit"
                                    ? $"task_edit:{task.Id}"
                                    : $"task_delete:{task.Id}")
                    })
                .ToList();

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            action switch
            {
                "finish" => "✅ اختر المهمة التي تريد إنهاءها:",
                "edit" => "✏️ اختر المهمة التي تريد تعديلها:",
                "delete" => "🗑 اختر المهمة التي تريد حذفها:",
                _ => "اختر المهمة:"
            },
            new InlineKeyboardMarkup(buttons),
            cancellationToken);
    }

    private async Task SendEmployeeSelectionForCreationAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var employees =
            await _telegram.GetEmployeesAsync(
                cancellationToken);

        if (employees.Count == 0)
        {
            await _telegram.SendMessageAsync(
                chatId,
                "❌ لا يوجد مستخدمون في النظام.",
                cancellationToken: cancellationToken);

            return;
        }

        var buttons =
            employees
                .Take(50)
                .Select(employee =>
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            $"👤 {employee.FullName ?? $"User #{employee.Id}"}",
                            $"assign_employee:{employee.Id}")
                    })
                .ToList();

        await _telegram.SendMessageAsync(
            chatId,
            "👤 اختر الموظف الذي تريد إسناد المهمة له:",
            new InlineKeyboardMarkup(buttons),
            cancellationToken);
    }

    // =========================================================
    // OVERDUE
    // =========================================================

    private async Task SendOverdueAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var tasks =
            await _telegram.GetOverdueTasksAsync(
                cancellationToken);

        if (!IsAdmin(user))
        {
            tasks =
                tasks
                    .Where(x =>
                        x.AssignedEmployeeId == user.Id)
                    .ToList();
        }

        if (tasks.Count == 0)
        {
            await _telegram.SendMessageAsync(
                user.ChatId!.Value,
                "✅ لا توجد مهام متأخرة.",
                cancellationToken: cancellationToken);

            return;
        }

        var text =
            "🔴 المهام المتأخرة\n\n" +
            string.Join(
                "\n\n",
                tasks.Select(
                    x =>
                        $"#{x.Id} {x.Content}\n" +
                        $"📅 الموعد: {x.DueDate:yyyy-MM-dd}\n" +
                        $"📍 {x.IsDoneOrNot ?? "لم تبدأ"}"));

        await _telegram.SendMessageAsync(
            user.ChatId!.Value,
            text,
            cancellationToken: cancellationToken);
    }

    // =========================================================
    // EMPLOYEES
    // =========================================================

    private async Task SendEmployeesAsync(
        User admin,
        CancellationToken cancellationToken)
    {
        var employees =
            await _telegram.GetEmployeesAsync(
                cancellationToken);

        var lines =
            new List<string>
            {
                "👥 مستخدمو REMS",
                ""
            };

        foreach (var employee in employees)
        {
            lines.Add(
                $"{(employee.IsAdmin ? "👑" : "👤")} " +
                $"#{employee.Id} " +
                $"{employee.FullName ?? "بدون اسم"}");

            lines.Add(
                $"Telegram: " +
                $"{(employee.ChatId.HasValue ? "✅" : "❌")}");

            lines.Add("");
        }

        await _telegram.SendMessageAsync(
            admin.ChatId!.Value,
            string.Join("\n", lines),
            cancellationToken: cancellationToken);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static bool IsAdmin(User user)
    {
        return user.IsAdmin ||
               user.IsFollowUpAdmin ||
               user.IsItAdmin;
    }

    private static int? ParseId(string text)
    {
        var parts =
            text.Split(
                ':',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return null;

        return int.TryParse(
            parts[^1],
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

    private sealed class BotState
    {
        public BotAction Action { get; set; }
        public int Step { get; set; }

        public string? Content { get; set; }

        public DateTime? DueDate { get; set; }

        public int? AssignedEmployeeId { get; set; }

        public int? TaskId { get; set; }
    }

    private enum BotAction
    {
        AddTask,
        EditTaskContent,
        Broadcast
    }
}