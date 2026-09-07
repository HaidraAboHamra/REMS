using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public sealed class TelegramDeliveryLog
{
    public long Id { get; set; }
    public long? ChatId { get; set; }
    public int? UserId { get; set; }

    [Required, MaxLength(80)]
    public string Operation { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [MaxLength(120)]
    public string? CorrelationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
