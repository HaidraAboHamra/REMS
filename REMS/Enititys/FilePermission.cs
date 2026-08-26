using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public class FilePermission
{
    [Key]
    public long Id { get; set; }

    public long FileId { get; set; }
    public int UserId { get; set; }

    // "view" or "edit"
    [Required, MaxLength(10)]
    public string Permission { get; set; } = "view";

    public int GrantedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual StoredFile? File { get; set; }
}
