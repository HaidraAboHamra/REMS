using Microsoft.EntityFrameworkCore;
using REMS.Data;

namespace REMS.Services;

/// <summary>
/// Keeps existing desktop SQLite databases compatible with file-manager
/// additions. The project predates EF migrations, so a narrowly scoped,
/// idempotent upgrade is safer than asking every installed copy to recreate
/// its database.
/// </summary>
public static class FileStorageSchemaInitializer
{
    public static async Task EnsureCurrentAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlite())
            return;

        var connection = db.Database.GetDbConnection();
        var closeConnection = connection.State != System.Data.ConnectionState.Open;

        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Users' LIMIT 1;";
            if (await tableCommand.ExecuteScalarAsync(cancellationToken) is null) return;

            await using (var userColumnsCommand = connection.CreateCommand())
            {
                userColumnsCommand.CommandText = "PRAGMA table_info('Users');";
                var userColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var reader = await userColumnsCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) userColumns.Add(reader.GetString(1));
                if (!userColumns.Contains("ProfileImagePath"))
                    await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN ProfileImagePath TEXT NULL;", cancellationToken);
                if (!userColumns.Contains("ProfileImageContentType"))
                    await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN ProfileImageContentType TEXT NULL;", cancellationToken);
                if (!userColumns.Contains("IsActive"))
                    await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;", cancellationToken);
            }

            await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS AuditLogs (Id INTEGER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY AUTOINCREMENT, ActorUserId INTEGER NULL, TargetUserId INTEGER NULL, Action TEXT NOT NULL, EntityType TEXT NULL, EntityId TEXT NULL, Description TEXT NOT NULL, IpAddress TEXT NULL, UserAgent TEXT NULL, CreatedAt TEXT NOT NULL);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_AuditLogs_TargetUserId_CreatedAt ON AuditLogs (TargetUserId, CreatedAt);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_AuditLogs_CreatedAt ON AuditLogs (CreatedAt);", cancellationToken);

            await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS AdminPermissionAssignments (Id INTEGER NOT NULL CONSTRAINT PK_AdminPermissionAssignments PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, PermissionKey TEXT NOT NULL, IsGranted INTEGER NOT NULL DEFAULT 1, UpdatedAt TEXT NOT NULL, UpdatedByUserId INTEGER NULL);", cancellationToken);
            await ExecuteAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS IX_AdminPermissionAssignments_UserId_PermissionKey ON AdminPermissionAssignments (UserId, PermissionKey);", cancellationToken);
            await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS TelegramBotSettings (Id INTEGER NOT NULL CONSTRAINT PK_TelegramBotSettings PRIMARY KEY, IsEnabled INTEGER NOT NULL DEFAULT 1, NotificationsEnabled INTEGER NOT NULL DEFAULT 1, BroadcastEnabled INTEGER NOT NULL DEFAULT 0, AllowTaskCreation INTEGER NOT NULL DEFAULT 1, AllowTaskEditing INTEGER NOT NULL DEFAULT 1, AllowTaskDeletion INTEGER NOT NULL DEFAULT 0, MaxBroadcastRecipients INTEGER NOT NULL DEFAULT 500, RetryCount INTEGER NOT NULL DEFAULT 3, LastHealthCheckAt TEXT NULL, LastHealthCheckSucceeded INTEGER NULL, LastHealthCheckMessage TEXT NULL, UpdatedAt TEXT NOT NULL, UpdatedByUserId INTEGER NULL);", cancellationToken);
            await ExecuteAsync(connection, "INSERT OR IGNORE INTO TelegramBotSettings (Id, IsEnabled, NotificationsEnabled, BroadcastEnabled, AllowTaskCreation, AllowTaskEditing, AllowTaskDeletion, MaxBroadcastRecipients, RetryCount, UpdatedAt) VALUES (1, 1, 1, 0, 1, 1, 0, 500, 3, CURRENT_TIMESTAMP);", cancellationToken);
            await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS TelegramDeliveryLogs (Id INTEGER NOT NULL CONSTRAINT PK_TelegramDeliveryLogs PRIMARY KEY AUTOINCREMENT, ChatId INTEGER NULL, UserId INTEGER NULL, Operation TEXT NOT NULL, Status TEXT NOT NULL, ErrorMessage TEXT NULL, CorrelationId TEXT NULL, CreatedAt TEXT NOT NULL);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_TelegramDeliveryLogs_Operation_CreatedAt ON TelegramDeliveryLogs (Operation, CreatedAt);", cancellationToken);

            await using var fileTableCommand = connection.CreateCommand();
            fileTableCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'FileFolders' LIMIT 1;";
            if (await fileTableCommand.ExecuteScalarAsync(cancellationToken) is null) return;

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using (var columnCommand = connection.CreateCommand())
            {
                columnCommand.CommandText = "PRAGMA table_info('FileFolders');";
                await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                    columns.Add(reader.GetString(1));
            }

            if (!columns.Contains("IsSharedHub"))
            {
                await using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText =
                    "ALTER TABLE FileFolders ADD COLUMN IsSharedHub INTEGER NOT NULL DEFAULT 0;";
                await alterCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText =
                "CREATE INDEX IF NOT EXISTS IX_FileFolders_IsSharedHub_ParentFolderId_Name " +
                "ON FileFolders (IsSharedHub, ParentFolderId, Name);";
            await indexCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task ExecuteAsync(System.Data.Common.DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
