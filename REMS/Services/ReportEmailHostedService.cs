using REMS.Interfaces;

public class ReportEmailHostedService(IServiceProvider _serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var _settings = scope.ServiceProvider.GetRequiredService<ISettings>();
            var currentTime = new TimeOnly(DateTime.Now.Hour, DateTime.Now.Minute);
            var timeToSendResult = await _settings.GetTimeOnlyToSendTheEmail();
            if (timeToSendResult.IsSuccess)
            {
                var timeToSend = timeToSendResult.Value;
                if (currentTime == timeToSend)
                {
                    await SendDailyReports();
                }
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    
    public async Task SendDailyReports()
    {
        using var scope = _serviceProvider.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        await reportService.SendDailyReports();
        await reportService.SendWeeklyComplaint();
    }
}
