using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using REMS.Enititys;

namespace REMS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Report> Reports { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Complaint> Complaints { get; set; }
    public DbSet<FollowUpReport> FollowUpReports { get; set; }
    public DbSet<Setting> Settings { get; set; }
    public DbSet<FormData> FormData { get; set; }
    public DbSet<Employees> Employees { get; set; }





    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FullName = "Admin", Email = "Admin@Admin.com", IsAdmin = true, PasswordHash = "AQAAAAIAAYagAAAAEORnOyHZWpGTFS206rXM8pdrBz/Y6pJVOVO8gnGRg6hlLw0VLtacH0ZIGx5Rk9/a0A==", PhoneNumber = "999", ChatId = 5505222952, IsFollowUpAdmin = false, IsItAdmin = false }
            );
        modelBuilder.Entity<Setting>().HasData(
            new Setting { Id = 1, Hour = 16, Minute = 30, NotificationTimeDifference = 15, SendTo = "alaa.ajelo@rexos.co" }
            );
      
        base.OnModelCreating(modelBuilder);
    }
}
