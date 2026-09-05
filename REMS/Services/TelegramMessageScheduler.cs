using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using REMS.Interfaces;

namespace REMS.Services;

public class TelegramMessageScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramMessageScheduler> _logger;

    private DateOnly? _lastSentDate;

    public TelegramMessageScheduler(
        IServiceProvider serviceProvider,
        ILogger<TelegramMessageScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _serviceProvider.CreateScope();

                var settings =
                    scope.ServiceProvider
                        .GetRequiredService<ISettings>();

                var result =
                    await settings.GetTimeOnlyToSendTheNotification();

                if (result.IsSuccess)
                {
                    var configuredTime =
                        result.Value;

                    var now =
                        DateTime.Now;

                    var currentTime =
                        new TimeOnly(
                            now.Hour,
                            now.Minute);

                    var today =
                        DateOnly.FromDateTime(now);

                    if (currentTime == configuredTime &&
                        _lastSentDate != today)
                    {
                        var telegram =
                            scope.ServiceProvider
                                .GetRequiredService<TelegramService>();

                        await telegram.SendDailyAssignedTasks(
                            stoppingToken);

                        _lastSentDate = today;

                        _logger.LogInformation(
                            "REMS daily Telegram notifications sent at {Time}.",
                            currentTime);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Telegram notification scheduler failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
