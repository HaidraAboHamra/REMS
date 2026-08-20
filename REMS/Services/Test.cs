using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using REMS.Interfaces;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

public class Test
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public Test(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public void TestGmail()
    {
        using var client = new SmtpClient(
            "smtp.gmail.com",
            587);

        client.EnableSsl = true;
        client.UseDefaultCredentials = false;

        client.Credentials = new NetworkCredential(
            "enghaidra@gmail.com",
            "foim zlfg auaf mtvo");

        client.DeliveryMethod =
            SmtpDeliveryMethod.Network;

        client.Timeout = 30000;

        using var message = new MailMessage();

        message.From = new MailAddress("enghaidra@gmail.com");

        message.To.Add("alyhaidra6@gmail.com");

        message.Subject = "REMS SMTP TEST";

        message.Body = "<h1>SMTP Test</h1>";

        message.IsBodyHtml = true;

        client.Send(message);
    }
    public async Task SendTryReport(string Email,DateTime date)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
            await reportService.SendWeeklyTaskReports(Email,date);
        }
    }
    public async Task SendTryFollowUpReport(string Email, DateTime date)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
            await reportService.SendDailyTaskReports(Email, date);
        }
    }

}
