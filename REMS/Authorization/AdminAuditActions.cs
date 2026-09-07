namespace REMS.Authorization;

public static class AdminAuditActions
{
    public const string SettingsUpdated = "AdminSettingsUpdated";
    public const string PermissionChanged = "AdminPermissionChanged";
    public const string TelegramTestSent = "TelegramTestSent";
    public const string TelegramBroadcastSent = "TelegramBroadcastSent";
    public const string SensitiveValueViewed = "AdminSensitiveValueViewed";
    public const string UserStatusChanged = "AdminUserStatusChanged";
}
