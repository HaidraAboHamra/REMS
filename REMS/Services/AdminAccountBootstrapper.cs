using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using REMS.Authorization;
using REMS.Data;
using REMS.Enititys;

namespace REMS.Services;

public static class AdminAccountBootstrapper
{
    private static readonly IReadOnlyList<string> FullPermissions = AdminPermissions.All;

    public static async Task GrantFullAccessAsync(
        AppDbContext db,
        IConfiguration configuration,
        bool isDevelopment,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
            return;

        var email = configuration["BootstrapAdminEmail"];
        var password = configuration["BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(email))
            return;
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdminPassword must be configured for development.");

        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Email != null && x.Email.ToLower() == email.Trim().ToLower(),
            cancellationToken);

        if (user is null)
        {
            user = new User
            {
                FullName = configuration["BootstrapAdminName"] ?? "Development Administrator",
                Email = email.Trim(),
                PasswordHash = string.Empty
            };
            db.Users.Add(user);
        }

        user.FullName = configuration["BootstrapAdminName"] ?? user.FullName;
        user.Email = email.Trim();
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        user.IsAdmin = true;
        user.IsItAdmin = true;
        user.IsFollowUpAdmin = true;
        user.IsFUser = true;

        foreach (var permission in FullPermissions)
        {
            var assignment = await db.AdminPermissionAssignments
                .SingleOrDefaultAsync(
                    x => x.UserId == user.Id && x.PermissionKey == permission,
                    cancellationToken);

            if (assignment is null)
            {
                db.AdminPermissionAssignments.Add(new AdminPermissionAssignment
                {
                    UserId = user.Id,
                    PermissionKey = permission,
                    IsGranted = true,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = user.Id
                });
            }
            else
            {
                assignment.IsGranted = true;
                assignment.UpdatedAt = DateTime.UtcNow;
                assignment.UpdatedByUserId = user.Id;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
