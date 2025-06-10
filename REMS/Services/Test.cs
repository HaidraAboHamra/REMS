using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using REMS.Interfaces;

public class Test
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public Test(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    
    public async Task SendTryReport(string Email,DateTime date)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
            await reportService.SendDailyReports(Email,date);
        }
    }
    public async Task SendTryFollowUpReport(string Email, DateTime date)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var reportService = scope.ServiceProvider.GetRequiredService<IFollowUpReportService>();
            await reportService.SendDailyReports(Email, date);
        }
    }

}
