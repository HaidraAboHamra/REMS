using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public class UserNote
{
    [Key]
    public long Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
