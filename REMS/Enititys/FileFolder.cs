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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual FileFolder? ParentFolder { get; set; }

    public virtual ICollection<FileFolder> Children { get; set; } = new List<FileFolder>();

    public virtual ICollection<StoredFile> Files { get; set; } = new List<StoredFile>();
}