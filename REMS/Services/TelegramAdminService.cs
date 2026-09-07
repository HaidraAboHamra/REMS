using Telegram.Bot;
using Microsoft.EntityFrameworkCore;
using REMS.Authorization;
using REMS.Data;
using REMS.DTOs;

namespace REMS.Services;

public sealed class TelegramAdminService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly AppDbContext _db;
    private readonly UserService _users;
    private readonly AuditLogService _audit;
    private readonly ILogger<TelegramAdminService> _logger;

    public TelegramAdminService(
        IConfiguration configuration,
        IServiceProvider services,
        AppDbContext db,
        UserService users,
        AuditLogService audit,
        ILogger<TelegramAdminService> logger)
    {
        _configuration = configuration;
        _services = services;
        _db = db;
        _users = users;
        _audit = audit;
        _logger = logger;
    }

    public string GetMaskedToken()
    {
        var token = _configuration["TelegramBotToken"];
        return string.IsNullOrWhiteSpace(token) ? "غير مضبوط" : $"••••••••{token[^4..]}";
    }

    public async Task<TelegramBotHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.UtcNow;
        var token = _configuration["TelegramBotToken"];
        if (string.IsNullOrWhiteSpace(token))
            return new(false, false, null, null, "رمز Telegram غير مضبوط.", checkedAt);

        try
        {
            var telegram = _services.GetService<TelegramService>();
            if (telegram is null)
                return new(true, false, null, null, "خدمة Telegram غير مفعلة.", checkedAt);

            var bot = await telegram.Client.GetMeAsync(cancellationToken);
            return new(true, true, bot.FirstName, bot.Username, "الاتصال بالبوت ناجح.", checkedAt);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Telegram health check failed");
            return new(true, false, null, null, "فشل الاتصال بواجهة Telegram.", checkedAt);
        }
    }

    public async Task<TelegramTokenRevealResult> RevealTokenAsync(
        int actorUserId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
        if (actor is null || string.IsNullOrWhiteSpace(actor.Email))
            return new(false, null, "تعذر التحقق من حساب الأدمن.");

        var authenticated = await _users.LoginAsync(actor.Email, password, cancellationToken);
        if (authenticated is null || (!authenticated.IsAdmin && !authenticated.IsFollowUpAdmin && !authenticated.IsItAdmin))
            return new(false, null, "كلمة المرور غير صحيحة.");

        var token = _configuration["TelegramBotToken"];
        if (string.IsNullOrWhiteSpace(token))
            return new(false, null, "رمز Telegram غير مضبوط.");

        await _audit.WriteAsync(AdminAuditActions.SensitiveValueViewed, "تم كشف رمز Telegram بعد إعادة التحقق.", actorUserId, "Configuration", "TelegramBotToken", cancellationToken: cancellationToken);
        return new(true, token, "تم التحقق بنجاح.");
    }
}
