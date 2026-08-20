using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.Interfaces;
using REMS.Services;
using System.Text;

namespace ReportApp.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly EmailService _email;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;

        public ReportService(
            AppDbContext context,
            EmailService email,
            IConfiguration config,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _email = email;
            _config = config;
            _serviceProvider = serviceProvider;
        }

        #region Reports القديمة

        public async Task<List<Report>> GetReportsByDate(DateTime date)
        {
            return await _context.Reports
                .AsNoTracking()
                .Where(r => r.DateTime.Date == date.Date)
                .ToListAsync();
        }

        public async Task<Report> AddReport(Report report)
        {
            report.DateTime = DateTime.Now;

            report.IsDoneOrNot = report.IsDone
                ? "منتهية"
                : "غير منتهية";

            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();

            return report;
        }

        #endregion

        #region FollowUpReport

        public async Task<FollowUpReport> AddFollowUpReport(
            FollowUpReport report)
        {
            report.DateTime = DateTime.Now;
            report.LastUpdatedDate = DateTime.Now;

            if (report.TotalItems > 0 &&
                report.CompletedItems >= report.TotalItems)
            {
                report.CompletedItems = report.TotalItems;
                report.IsDone = true;
                report.IsDoneOrNot = "مكتملة";

                if (!report.CompletedDate.HasValue)
                {
                    report.CompletedDate = DateTime.Now;
                }
            }
            else if (report.CompletedItems > 0)
            {
                report.IsDone = false;
                report.IsDoneOrNot = "قيد التنفيذ";
            }
            else
            {
                report.IsDone = false;
                report.IsDoneOrNot = "لم تبدأ";
            }

            await _context
                .Set<FollowUpReport>()
                .AddAsync(report);

            await _context.SaveChangesAsync();

            return report;
        }

        #endregion

        #region إرسال بريد المهمة للموظف المسؤول

        public async Task SendTaskAssignmentEmail(
            FollowUpReport report)
        {
            if (!report.AssignedEmployeeId.HasValue)
            {
                return;
            }

            var employee = await _context
                .Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == report.AssignedEmployeeId.Value);

            if (employee == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(employee.Email))
            {
                return;
            }

            await _email.SendTaskAssignmentEmailAsync(
                report,
                employee);
        }

        #endregion

        #region التقرير اليومي

        public async Task SendDailyTaskReports()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var reports = await _context
                .Set<FollowUpReport>()
                .AsNoTracking()
                .Where(x =>
                    (
                        x.DateTime >= today &&
                        x.DateTime < tomorrow
                    )
                    ||
                    (
                        x.LastUpdatedDate.HasValue &&
                        x.LastUpdatedDate.Value >= today &&
                        x.LastUpdatedDate.Value < tomorrow
                    ))
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Priority)
                .ToListAsync();

            if (!reports.Any())
            {
                return;
            }

            using var scope =
                _serviceProvider.CreateScope();

            var settings =
                scope.ServiceProvider
                    .GetRequiredService<ISettings>();

            var emailResult =
                await settings.GetEmail();

            var email =
                emailResult.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            await _email.SendDailyTaskReportAsync(
                email,
                reports,
                today);
        }

        #endregion

        #region التقرير اليومي لبريد محدد

        public async Task SendDailyTaskReports(
            string email,
            DateTime date)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "البريد الإلكتروني غير صالح.",
                    nameof(email));
            }

            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            var reports = await _context
                .Set<FollowUpReport>()
                .AsNoTracking()
                .Where(x =>
                    (
                        x.DateTime >= startDate &&
                        x.DateTime < endDate
                    )
                    ||
                    (
                        x.LastUpdatedDate.HasValue &&
                        x.LastUpdatedDate.Value >= startDate &&
                        x.LastUpdatedDate.Value < endDate
                    ))
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Priority)
                .ToListAsync();

            if (!reports.Any())
            {
                return;
            }

            await _email.SendDailyTaskReportAsync(
                email,
                reports,
                startDate);
        }

        #endregion

        #region التقرير الأسبوعي

        public async Task SendWeeklyTaskReports()
        {
            var today = DateTime.Today;

            int diff =
                (7 +
                 (
                     today.DayOfWeek -
                     DayOfWeek.Saturday
                 )) % 7;

            var weekStart =
                today.AddDays(-diff).Date;

            var weekEnd =
                weekStart.AddDays(7);

            var reports = await _context
                .Set<FollowUpReport>()
                .AsNoTracking()
                .Where(x =>
                    (
                        x.DateTime >= weekStart &&
                        x.DateTime < weekEnd
                    )
                    ||
                    (
                        x.LastUpdatedDate.HasValue &&
                        x.LastUpdatedDate.Value >= weekStart &&
                        x.LastUpdatedDate.Value < weekEnd
                    )
                    ||
                    (
                        x.DueDate.HasValue &&
                        x.DueDate.Value >= weekStart &&
                        x.DueDate.Value < weekEnd
                    ))
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Priority)
                .ToListAsync();

            if (!reports.Any())
            {
                return;
            }

            using var scope =
                _serviceProvider.CreateScope();

            var settings =
                scope.ServiceProvider
                    .GetRequiredService<ISettings>();

            var emailResult =
                await settings.GetEmail();

            var email =
                emailResult.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            await _email.SendWeeklyTaskReportAsync(
                email,
                reports,
                weekStart,
                weekStart.AddDays(6));
        }

        #endregion

        #region التقرير الأسبوعي لبريد محدد

        public async Task SendWeeklyTaskReports(
            string email,
            DateTime weekStart)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "البريد الإلكتروني غير صالح.",
                    nameof(email));
            }

            weekStart = weekStart.Date;

            var weekEnd =
                weekStart.AddDays(7);

            var reports = await _context
                .Set<FollowUpReport>()
                .AsNoTracking()
                .Where(x =>
                    (
                        x.DateTime >= weekStart &&
                        x.DateTime < weekEnd
                    )
                    ||
                    (
                        x.LastUpdatedDate.HasValue &&
                        x.LastUpdatedDate.Value >= weekStart &&
                        x.LastUpdatedDate.Value < weekEnd
                    )
                    ||
                    (
                        x.DueDate.HasValue &&
                        x.DueDate.Value >= weekStart &&
                        x.DueDate.Value < weekEnd
                    ))
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Priority)
                .ToListAsync();

            if (!reports.Any())
            {
                return;
            }

            await _email.SendWeeklyTaskReportAsync(
                email,
                reports,
                weekStart,
                weekStart.AddDays(6));
        }

        #endregion

        #region إضافة شكوى

        public async Task<Complaint> AddComplaint(
            Complaint complaint)
        {
            complaint.DateTime = DateTime.Now;

            await _context.Complaints
                .AddAsync(complaint);

            await _context.SaveChangesAsync();

            return complaint;
        }

        #endregion

        #region التقرير الأسبوعي للشكاوى

        public async Task SendWeeklyComplaint()
        {
            var today = DateTime.Today;

            int diff =
                (7 +
                 (
                     today.DayOfWeek -
                     DayOfWeek.Saturday
                 )) % 7;

            var weekStart =
                today.AddDays(-diff).Date;

            var weekEnd =
                weekStart.AddDays(7);

            var complaints = await _context.Complaints
                .AsNoTracking()
                .Where(x =>
                    x.DateTime >= weekStart &&
                    x.DateTime < weekEnd)
                .OrderByDescending(x => x.DateTime)
                .ToListAsync();

            if (!complaints.Any())
            {
                return;
            }

            var email = _config["SendTo"];

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var reportContent =
                new StringBuilder();

            #region HTML Header

            reportContent.Append(@"
<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">

<head>
    <meta charset=""UTF-8"">
</head>

<body style=""
    margin:0;
    padding:20px;
    background:#f4f6f9;
    font-family:Arial,Tahoma,sans-serif;
"">

<div style=""
    max-width:900px;
    margin:auto;
    background:#fff;
    padding:25px;
    border-radius:12px;
"">

<h2 style=""color:#222;"">
    التقرير الأسبوعي للشكاوى
</h2>

<p style=""color:#666;"">
    من ");

            reportContent.Append(
                weekStart.ToString("yyyy-MM-dd"));

            reportContent.Append(
                " إلى ");

            reportContent.Append(
                weekStart
                    .AddDays(6)
                    .ToString("yyyy-MM-dd"));

            reportContent.Append(@"
</p>

<table style=""
    width:100%;
    border-collapse:collapse;
"">

<thead>

<tr>

<th style=""
    padding:12px;
    border:1px solid #ddd;
    background:#f1f3f5;
"">
    الشكوى
</th>

<th style=""
    padding:12px;
    border:1px solid #ddd;
    background:#f1f3f5;
"">
    التاريخ
</th>

</tr>

</thead>

<tbody>
");

            #endregion

            #region Complaints Rows

            foreach (var complaint in complaints)
            {
                reportContent.Append($@"

<tr>

<td style=""
    padding:12px;
    border:1px solid #ddd;
"">
    {System.Net.WebUtility.HtmlEncode(
        complaint.Content ?? "-")}
</td>

<td style=""
    padding:12px;
    border:1px solid #ddd;
"">
    {complaint.DateTime:yyyy-MM-dd}
</td>

</tr>
");
            }

            #endregion

            #region HTML Footer

            reportContent.Append(@"
</tbody>

</table>

</div>

</body>

</html>
");

            #endregion

            await _email.SendEmailAsync(
                email,
                "REMS | التقرير الأسبوعي للشكاوى",
                reportContent.ToString());
        }

        #endregion

        #region Backward Compatibility

        public Task SendDailyReports()
        {
            throw new NotImplementedException();
        }

        public Task SendDailyReports(
            string email,
            DateTime date)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}