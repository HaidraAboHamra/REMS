namespace REMS.DTOs;

public sealed record TelegramBotHealth(
    bool IsConfigured,
    bool IsReachable,
    string? BotName,
    string? BotUsername,
    string Message,
    DateTime CheckedAtUtc);

public sealed record TelegramDeliveryLogDto(
    long Id,
    string Operation,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    long? ChatId);

public sealed record TelegramTokenRevealResult(bool Succeeded, string? Token, string Message);
