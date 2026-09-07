namespace REMS.Services;

public sealed class AuditLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditLogCleanupService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AuditLogService>().RemoveExpiredAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}