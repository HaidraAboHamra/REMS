using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public sealed class TelegramBotSettings
{
    [Key]
    public int Id { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public bool BroadcastEnabled { get; set; }
    public bool AllowTaskCreation { get; set; } = true;
    public bool AllowTaskEditing { get; set; } = true;
    public bool AllowTaskDeletion { get; set; }
    public int MaxBroadcastRecipients { get; set; } = 500;
    public int RetryCount { get; set; } = 3;
    public DateTime? LastHealthCheckAt { get; set; }
    public bool? LastHealthCheckSucceeded { get; set; }
    public string? LastHealthCheckMessage { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}
