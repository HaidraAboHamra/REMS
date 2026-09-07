using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using REMS.Enititys;

namespace REMS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Report> Reports { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserNote> UserNotes { get; set; }
    public DbSet<Complaint> Complaints { get; set; }
    public DbSet<FollowUpReport> FollowUpReports { get; set; }
    public DbSet<Setting> Settings { get; set; }
    public DbSet<FormData> FormData { get; set; }
    public DbSet<Employees> Employees { get; set; }

    public DbSet<FollowUpReportUpdate> FollowUpReportUpdates { get; set; }
    public DbSet<FileFolder> FileFolders { get; set; }

    public DbSet<StoredFile> StoredFiles { get; set; }
    public DbSet<FilePermission> FilePermissions { get; set; }
    public DbSet<FileShareLink> FileShareLinks { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FullName = "Admin", Email = "Admin@Admin.com", IsAdmin = false, PasswordHash = "AQAAAAIAAYagAAAAEORnOyHZWpGTFS206rXM8pdrBz/Y6pJVOVO8gnGRg6hlLw0VLtacH0ZIGx5Rk9/a0A==", PhoneNumber = "999", ChatId = 00000, IsFollowUpAdmin = true, IsItAdmin = false }
            );
        modelBuilder.Entity<Setting>().HasData(
            new Setting { Id = 1, Hour = 16, Minute = 30, NotificationTimeDifference = 15, SendTo = "hexstudio.marketing@gmail.com" }
            );
        modelBuilder.Entity<FileFolder>()
      .HasOne(x => x.ParentFolder)
      .WithMany(x => x.Children)
      .HasForeignKey(x => x.ParentFolderId)
      .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StoredFile>()
            .HasOne(x => x.Folder)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FileFolder>()
            .HasIndex(x => new
            {
                x.OwnerId,
                x.ParentFolderId,
                x.Name
            });

        modelBuilder.Entity<FileFolder>()
            .HasIndex(x => new
            {
                x.IsSharedHub,
                x.ParentFolderId,
                x.Name
            });

        modelBuilder.Entity<StoredFile>()
            .HasIndex(x => new
            {
                x.OwnerId,
                x.FolderId
            });

        modelBuilder.Entity<StoredFile>()
            .HasIndex(x => new { x.IsSharedHub, x.CreatedAt });

        modelBuilder.Entity<AuditLog>()
            .HasIndex(x => new { x.TargetUserId, x.CreatedAt });

        modelBuilder.Entity<AuditLog>()
            .HasIndex(x => x.CreatedAt);
        base.OnModelCreating(modelBuilder);
    }
}
