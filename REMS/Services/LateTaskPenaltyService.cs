using Microsoft.EntityFrameworkCore;
using REMS.Data;

namespace REMS.Services;

public sealed class LateTaskPenaltyService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<LateTaskPenaltyService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ApplyPenaltiesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = DateTime.Today.AddDays(1).AddMinutes(5);
            var delay = nextRun - DateTime.Now;
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1), stoppingToken);
            await ApplyPenaltiesAsync(stoppingToken);
        }
    }

    private async Task ApplyPenaltiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var setting = await db.Settings
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var penaltyAmount = setting?.LatePenaltyAmount ?? 0m;

            if (penaltyAmount <= 0)
                return;

            var overdueTasks = await db.FollowUpReports
                .Where(x =>
                    !x.IsDone &&
                    !x.LatePenaltyApplied &&
                    x.AssignedEmployeeId.HasValue &&
                    x.DueDate.HasValue &&
                    x.DueDate.Value.Date < DateTime.Today)
                .ToListAsync(cancellationToken);

            var userIds = overdueTasks
                .Where(x => x.AssignedEmployeeId.HasValue)
                .Select(x => x.AssignedEmployeeId!.Value)
                .Distinct()
                .ToList();

            var users = await db.Users
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var task in overdueTasks)
            {
                if (!users.TryGetValue(task.AssignedEmployeeId!.Value, out var user))
                    continue;

                user.TotalDeductions += penaltyAmount;
                task.LatePenaltyApplied = true;
                task.LatePenaltyAmount = penaltyAmount;
                task.LatePenaltyAppliedAt = DateTime.UtcNow;
            }

            if (overdueTasks.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply late task penalties.");
        }
    }
}
