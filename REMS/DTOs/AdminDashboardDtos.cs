namespace REMS.DTOs;

public sealed record AdminDashboardSnapshot(
    int TotalUsers,
    int LinkedTelegramUsers,
    int TotalTasks,
    int OpenTasks,
    int OverdueTasks,
    int CompletedTasks,
    int ActiveUsers,
    int InactiveUsers,
    int UnlinkedTelegramUsers,
    DateTime GeneratedAtUtc);

public sealed record AdminPermissionDto(
    int UserId,
    string PermissionKey,
    bool IsGranted);

public sealed record AdminPermissionMatrixRow(
    int UserId,
    string DisplayName,
    IReadOnlyDictionary<string, bool> Permissions);

public sealed record AdminDecisionSummary(
    int AtRiskTasks,
    int CriticalTasks,
    int UnlinkedActiveUsers,
    string Recommendation);
