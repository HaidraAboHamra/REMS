using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys;

public class FileFolder
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(180)]
    public string Name { get; set; } = string.Empty;

    public int OwnerId { get; set; }

    public int? ParentFolderId { get; set; }

    /// <summary>
    /// True when this folder belongs to the company-wide Hub. Hub folders are
    /// visible to every signed-in user, while only Hub administrators can
    /// create, rename, or remove them.
    /// </summary>
    public bool IsSharedHub { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual FileFolder? ParentFolder { get; set; }

    public virtual ICollection<FileFolder> Children { get; set; } = new List<FileFolder>();

    public virtual ICollection<StoredFile> Files { get; set; } = new List<StoredFile>();
}
