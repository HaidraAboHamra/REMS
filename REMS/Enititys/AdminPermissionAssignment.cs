using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public sealed class AdminPermissionAssignment
{
    public long Id { get; set; }
    public int UserId { get; set; }

    [Required, MaxLength(120)]
    public string PermissionKey { get; set; } = string.Empty;

    public bool IsGranted { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}
