using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using REMS.Interfaces;
using REMS.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
 namespace REMS.Services;
public class TelegramMessageScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramMessageScheduler> _logger;
    private readonly IConfiguration _configuration;

    //private readonly TimeSpan _sendTime = new TimeSpan(11, 37, 0);

    public TelegramMessageScheduler(IServiceProvider serviceProvider, ILogger<TelegramMessageScheduler> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {

            using var scope = _serviceProvider.CreateScope();
            var _settings = scope.ServiceProvider.GetRequiredService<ISettings>();
            var currentTime = new TimeOnly(DateTime.Now.Hour, DateTime.Now.Minute);
            var timeToSendResult = await _settings.GetTimeOnlyToSendTheNotification();
            if (timeToSendResult.IsSuccess)
            {
                var timeToSend = timeToSendResult.Value;
                 if (currentTime == timeToSend)
                {
                    try
                    {
                    var telegramService = scope.ServiceProvider.GetRequiredService<TelegramService>();
                    await telegramService.SendMessagesToAllUsers("يرجى اضافة تقرير الى REMS");

                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
