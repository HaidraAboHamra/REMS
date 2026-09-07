using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public class AuditLog
{
    [Key]
    public long Id { get; set; }

    public int? ActorUserId { get; set; }
    public int? TargetUserId { get; set; }

    [Required, MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityType { get; set; }

    [MaxLength(80)]
    public string? EntityId { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}