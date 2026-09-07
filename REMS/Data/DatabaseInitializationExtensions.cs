using Microsoft.EntityFrameworkCore;
using REMS.Services;

namespace REMS.Data;

/// <summary>
/// Contains the startup database initialization used by the desktop deployment.
/// Keeping this outside Program.cs makes the startup pipeline easier to read and test.
/// </summary>
public static class DatabaseInitializationExtensions
{
    public static async Task InitializeRemsDatabaseAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        await using var scope = services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        // Existing installations may have a migration history, while older local
        // installations may only have a database created by EnsureCreated.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        await FileStorageSchemaInitializer.EnsureCurrentAsync(db);
        await AdminAccountBootstrapper.GrantFullAccessAsync(db, configuration, isDevelopment);
    }
}
