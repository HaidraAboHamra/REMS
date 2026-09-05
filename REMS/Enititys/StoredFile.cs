using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace REMS.Enititys;

public class StoredFile
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(255)]
    public string OriginalName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string StoredName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RelativePath { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ContentType { get; set; }

    public long Size { get; set; }
    public int OwnerId { get; set; }
    public int? FolderId { get; set; }

    /// <summary>
    /// A file published by an administrator in the company-wide hub.
    /// Hub files are readable by every authenticated REMS user.
    /// </summary>
    public bool IsSharedHub { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string OwnerName { get; set; } = string.Empty;

    public virtual FileFolder? Folder { get; set; }
    public virtual ICollection<FilePermission> Permissions { get; set; } = new List<FilePermission>();
    public virtual ICollection<FileShareLink> ShareLinks { get; set; } = new List<FileShareLink>();
}
