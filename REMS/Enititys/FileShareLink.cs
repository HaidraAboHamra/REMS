using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public class FileShareLink
{
    [Key]
    public long Id { get; set; }

    public long FileId { get; set; }

    [Required, MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    public virtual StoredFile? File { get; set; }
}
