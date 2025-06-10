using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.Interfaces;
using REMS.Services;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace ReportApp.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly EmailService _email;
        private readonly IConfiguration _config;
        IServiceProvider _serviceProvider;
        public ReportService(AppDbContext context, EmailService email, IConfiguration config,IServiceProvider serviceProvider)
        {
            _context = context;
            _email = email;
            _config = config;
            _serviceProvider = serviceProvider;
        }
        public async Task<List<Report>> GetReportsByDate(DateTime date)
        {
            return await _context.Reports
                .Where(r => r.DateTime.Date == date.Date)
                .ToListAsync();
        }
        public async Task<Report> AddReport(Report report)
        {
            report.DateTime = DateTime.Now;
            if (report.IsDone == true)
            {
                report.IsDoneOrNot = "منتهية";
            }
            else {
                report.IsDoneOrNot = "غير منتهية";
            }
            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();
            return report;
        }

        public async Task SendDailyReports()
        {
            
            var todayReports = await _context.Reports
                .Where(r => r.DateTime.Date == DateTime.Today)
                .ToListAsync();
            using var scope = _serviceProvider.CreateScope();
            var _settings = scope.ServiceProvider.GetRequiredService<ISettings>();
            var timeToSendResult = await _settings.GetEmail();
            if (todayReports.Any())
            {
                var reportContent = new StringBuilder();
                reportContent.Append("<!DOCTYPE html><html><body style='direction: rtl;'><table border='1' cellpadding='5' cellspacing='0'>");
                reportContent.Append("<thead><tr><th>اسم الموظف</th><th>المهمة</th><th>التاريخ</th><th>الحالة</th></tr></thead>");
                reportContent.Append("<tbody>");

                foreach (var report in todayReports)
                {
                    reportContent.AppendFormat(
                        "<tr><td style='text-align: right;'>{0}</td><td style='text-align: right;'>{1}</td><td style='text-align: right;'>{2}</td><td style='text-align: right;'>{3}</td></tr>",
                        report.FullName,
                        report.Content,
                        report.DateTime.ToString("yyyy-MM-dd"),
                        report.IsDoneOrNot
                    );
                }

                reportContent.Append("</tbody></table></body></html>");

                _email.SendEmail(timeToSendResult.Value!, "تقرير قسم البرمجة", reportContent.ToString());
            }

        }
        public async Task SendDailyReports(string email, DateTime Date)
        {
            var todayReports = await _context.Reports
                .Where(r => r.DateTime.Date ==Date.Date)
                .ToListAsync();

            if (todayReports.Any())
            {
                var reportContent = new StringBuilder();
                reportContent.Append("<!DOCTYPE html><html><body style='direction: rtl;'><table border='1' cellpadding='5' cellspacing='0'>");
                reportContent.Append("<thead><tr><th>اسم الموظف</th><th>المهمة</th><th>التاريخ</th><th>الحالة</th></tr></thead>");
                reportContent.Append("<tbody>");

                foreach (var report in todayReports)
                {
                    reportContent.AppendFormat(
                        "<tr><td style='text-align: right;'>{0}</td><td style='text-align: right;'>{1}</td><td style='text-align: right;'>{2}</td><td style='text-align: right;'>{3}</td></tr>",
                        report.FullName,
                        report.Content,
                        report.DateTime.ToString("yyyy-MM-dd"),
                        report.IsDoneOrNot
                    );
                }

                reportContent.Append("</tbody></table></body></html>");

                _email.SendEmail(email!, "Daily Reports", reportContent.ToString());
            }

        }

        public async Task SendWeeklyComplaint()
        {

            var WeekComplaints = await _context.Complaints
                .Where(r => r.DateTime.Date == DateTime.Today)
                .ToListAsync();

            if (WeekComplaints.Any())
            {
                var reportContent = new StringBuilder();
                reportContent.Append("<!DOCTYPE html><html><body style='direction: rtl;'><table border='1' cellpadding='5' cellspacing='0'>");
                reportContent.Append("<thead><tr><th>الشكوى</th><th>التاريخ</th></thead>");
                reportContent.Append("<tbody>");

                foreach (var report in WeekComplaints)
                {
                    reportContent.AppendFormat(
                        "<tr><td style='text-align: right;'>{0}</td><td style='text-align: right;'>{1}</td></tr>",
                       
                        report.Content,
                        report.DateTime.ToString("yyyy-MM-dd")

                    );
                }

                reportContent.Append("</tbody></table></body></html>");

                _email.SendEmail(_config["SendTo"]!, "Daily Reports", reportContent.ToString());
            }
        }
        public async Task<Complaint> AddComplaint(Complaint complaint)
        {
            complaint.DateTime = DateTime.Now;
          
            await _context.Complaints.AddAsync(complaint);
            await _context.SaveChangesAsync();
            return complaint;
        }

    }
}
