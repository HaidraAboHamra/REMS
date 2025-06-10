using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.Interfaces;
using System.Net.Mail;
using System.Text;

namespace REMS.Services
{
    public class FollowUpReportService : IFollowUpReportService
    {
        private readonly AppDbContext _context;
        private readonly EmailService _email;
        private readonly IConfiguration _config;
        public FollowUpReportService(AppDbContext context, EmailService email, IConfiguration config)
        {
            _context = context;
            _email = email;
            _config = config;
        }

        public async Task<Complaint> AddComplaint(Complaint complaint)
        {
            complaint.DateTime = DateTime.Now;
            await _context.Complaints.AddAsync(complaint);
            await _context.SaveChangesAsync();
            return complaint;
        }

        public async Task<FollowUpReport> AddReport(FollowUpReport report)
        {
            report.DateTime = DateTime.Now;
            if (report.IsDone == true)
            {
                report.IsDoneOrNot = "منتهية";
            }
            else
            {
                report.IsDoneOrNot = "غير منتهية";
            }
            _context.FollowUpReports.Add(report);
            await _context.SaveChangesAsync();
            return report;
        }

        public async Task<bool> Delete(int Id)
        {
            try
            {
                var report = _context.FollowUpReports.FirstOrDefault(x => x.Id == Id);
                if (report != null)
                {
                    if (report.Path != string.Empty)
                        if (!await DeleteFile(report.Path!))
                            return false;

                    _context.Remove(report);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteFile(string Path)
        {
            try
            {
                File.Delete(Path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FollowUpReport> Get(int Id)
        {
            var FollowUpReport = await _context.FollowUpReports.FindAsync(Id);
            return FollowUpReport!;
        }

        public async Task<List<FollowUpReport>> GetReportsByDate(DateTime date)
        {
            return await _context.FollowUpReports
                .Where(r => r.DateTime.Date == date.Date)
                .ToListAsync();
        }

        public async Task SendDailyReports(string email, DateTime date)
        {
            var todayReports = await _context.FollowUpReports
                .Where(r => r.DateTime.Date == date)
                .ToListAsync();

            if (todayReports.Any())
            {
                var reportContent = new StringBuilder();
                reportContent.Append("<!DOCTYPE html><html><body style='direction: rtl;'><table border='1' cellpadding='5' cellspacing='0'>");
                reportContent.Append("<thead><tr><th>اسم الموظف</th><th>المهمة</th><th>التاريخ</th><th>الحالة</th><th>الملف</th></tr></thead>");
                reportContent.Append("<tbody>");

                foreach (var report in todayReports)
                {
                    string Split(string path)
                    {
                        if (string.IsNullOrEmpty(path))
                            return string.Empty;

                        var fileName = System.IO.Path.GetFileName(path);

                        return fileName;
                    }
                    string fileLink = string.IsNullOrEmpty(report.Path) ? "لا يوجد ملف" : $"{Split(report.Path)}";
                    reportContent.AppendFormat(
                        "<tr><td style='text-align: right;'>{0}</td><td style='text-align: right;'>{1}</td><td style='text-align: right;'>{2}</td><td style='text-align: right;'>{3}</td><td style='text-align: right;'>{4}</td></tr>",
                        report.FullName,
                        report.Content,
                        report.DateTime.ToString("yyyy-MM-dd"),
                        report.IsDoneOrNot,
                        fileLink
                    );
                }
                reportContent.Append("</tbody></table></body></html>");

                var mailMessage = new MailMessage();
                mailMessage.To.Add(email);
                mailMessage.Subject = "تقرير قسم المتابعة";
                mailMessage.Body = reportContent.ToString();
                mailMessage.IsBodyHtml = true;

                foreach (var report in todayReports)
                {
                    if (!string.IsNullOrEmpty(report.Path))
                    {
                        Attachment attachment = new Attachment(report.Path);
                        mailMessage.Attachments.Add(attachment);
                    }
                }

                _email.SendEmail(email, "تقرير قسم المتابعة", reportContent.ToString(), mailMessage.Attachments);
            }
        }

        public Task SendWeeklyComplaint()
        {
            throw new NotImplementedException();
        }

        public async Task<FollowUpReport> Update(FollowUpReport report)
        {
            var temp = await _context.FollowUpReports.FindAsync(report.Id);
            temp.IsDoneOrNot = report.IsDoneOrNot;
            temp.IsDone = report.IsDone;
            temp.Content = report.Content;
            _context.FollowUpReports.Update(temp);
            await _context.SaveChangesAsync();
            return temp;
        }
        public async Task<FollowUpReport> Update(FollowUpReport report, string newPath)
        {
            var temp = await _context.FollowUpReports.FindAsync(report.Id);
            var test = await DeleteFile(temp.Path);
            temp.Path = newPath;
            temp.IsDoneOrNot = report.IsDoneOrNot;
            temp.IsDone = report.IsDone;
            temp.Content = report.Content;
            _context.FollowUpReports.Update(temp);
            await _context.SaveChangesAsync();
            return temp;
        }
    }
}
