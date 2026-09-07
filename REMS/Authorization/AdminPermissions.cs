namespace REMS.Authorization;

public static class AdminPermissions
{
    public const string DashboardView = "admin.dashboard.view";
    public const string SettingsManage = "admin.settings.manage";
    public const string TasksManage = "admin.tasks.manage";
    public const string UsersManage = "admin.users.manage";
    public const string TelegramManage = "admin.telegram.manage";
    public const string TelegramBroadcast = "admin.telegram.broadcast";
    public const string AuditView = "admin.audit.view";
    public const string SensitiveActions = "admin.sensitive.actions";
    public const string UserStatusManage = "admin.users.status.manage";
    public const string HealthView = "admin.health.view";

    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView,
        SettingsManage,
        TasksManage,
        UsersManage,
        TelegramManage,
        TelegramBroadcast,
        AuditView,
        SensitiveActions
        ,UserStatusManage,
        HealthView
    ];
}

public sealed record AdminPermissionRequirement(string Permission) : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement;
