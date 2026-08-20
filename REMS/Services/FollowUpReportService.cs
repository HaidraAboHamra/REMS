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

        public FollowUpReportService(
            AppDbContext context,
            EmailService email,
            IConfiguration config)
        {
            _context = context;
            _email = email;
            _config = config;
        }


        // =========================================================
        // إضافة شكوى
        // =========================================================

        public async Task<Complaint> AddComplaint(
            Complaint complaint)
        {
            complaint.DateTime = DateTime.Now;

            await _context.Complaints.AddAsync(complaint);

            await _context.SaveChangesAsync();

            return complaint;
        }


        // =========================================================
        // إضافة مهمة جديدة
        // =========================================================

        public async Task<FollowUpReport> AddReport(
            FollowUpReport report)
        {
            report.DateTime = DateTime.Now;

            // التأكد من صحة الأعداد
            if (report.TotalItems < 0)
                report.TotalItems = 0;

            if (report.CompletedItems < 0)
                report.CompletedItems = 0;

            if (report.CompletedItems > report.TotalItems)
                report.CompletedItems = report.TotalItems;


            // حساب الحالة
            SetTaskStatus(report);


            // بيانات التحديث
            report.LastUpdatedDate = DateTime.Now;

            if (string.IsNullOrWhiteSpace(report.LastUpdatedBy))
            {
                report.LastUpdatedBy = report.FullName;
            }


            await _context.FollowUpReports.AddAsync(report);

            await _context.SaveChangesAsync();


            // =====================================================
            // تسجيل أول حالة في سجل التحديثات
            // =====================================================

            var firstUpdate = new FollowUpReportUpdate
            {
                FollowUpReportId = report.Id,

                CompletedItems = report.CompletedItems,

                CompletedDetails = report.CompletedDetails,

                Status = report.IsDoneOrNot,

                ProgressPercentage = report.ProgressPercentage,

                DateTime = DateTime.Now,

                UpdatedBy = report.FullName
            };


            await _context.FollowUpReportUpdates.AddAsync(
                firstUpdate);

            await _context.SaveChangesAsync();


            return report;
        }


        // =========================================================
        // حساب حالة المهمة
        // =========================================================

        private void SetTaskStatus(
            FollowUpReport report)
        {
            if (report.TotalItems <= 0)
            {
                report.IsDone = false;
                report.IsDoneOrNot = "لم تبدأ";

                return;
            }


            if (report.CompletedItems <= 0)
            {
                report.CompletedItems = 0;

                report.IsDone = false;

                report.IsDoneOrNot = "لم تبدأ";

                return;
            }


            if (report.CompletedItems < report.TotalItems)
            {
                report.IsDone = false;

                report.IsDoneOrNot = "قيد التنفيذ";

                report.CompletedDate = null;

                return;
            }


            // جميع البنود انتهت
            report.CompletedItems =
                report.TotalItems;

            report.IsDone = true;

            report.IsDoneOrNot = "مكتملة";

            if (report.CompletedDate == null)
            {
                report.CompletedDate = DateTime.Now;
            }
        }


        // =========================================================
        // جلب مهمة
        // =========================================================

        public async Task<FollowUpReport> Get(int Id)
        {
            var report =
                await _context.FollowUpReports
                    .FirstOrDefaultAsync(x => x.Id == Id);

            return report!;
        }


        // =========================================================
        // جلب المهام حسب التاريخ
        // =========================================================

        public async Task<List<FollowUpReport>> GetReportsByDate(
            DateTime date)
        {
            return await _context.FollowUpReports
                .Where(r => r.DateTime.Date == date.Date)
                .OrderByDescending(r => r.DateTime)
                .ToListAsync();
        }


        // =========================================================
        // تحديث المهمة
        // =========================================================

        public async Task<FollowUpReport> Update(
            FollowUpReport report)
        {
            var existingReport =
                await _context.FollowUpReports
                    .FirstOrDefaultAsync(
                        x => x.Id == report.Id
                    );


            if (existingReport == null)
            {
                throw new Exception(
                    "المهمة غير موجودة");
            }


            // ==============================================
            // حفظ البيانات الجديدة
            // ==============================================

            existingReport.CompletedItems =
                report.CompletedItems;

            existingReport.CompletedDetails =
                report.CompletedDetails;


            // التأكد أن العدد صحيح
            if (existingReport.CompletedItems < 0)
            {
                existingReport.CompletedItems = 0;
            }


            if (existingReport.CompletedItems >
                existingReport.TotalItems)
            {
                existingReport.CompletedItems =
                    existingReport.TotalItems;
            }


            // ==============================================
            // تحديث الحالة
            // ==============================================

            SetTaskStatus(existingReport);


            // ==============================================
            // بيانات آخر تحديث
            // ==============================================

            existingReport.LastUpdatedDate =
                DateTime.Now;

            existingReport.LastUpdatedBy =
                report.LastUpdatedBy;


            // ==============================================
            // حفظ المهمة
            // ==============================================

            await _context.SaveChangesAsync();


            // ==============================================
            // إنشاء سجل تحديث
            // ==============================================

            var history =
                new FollowUpReportUpdate
                {
                    FollowUpReportId =
                        existingReport.Id,

                    CompletedItems =
                        existingReport.CompletedItems,

                    CompletedDetails =
                        existingReport.CompletedDetails,

                    Status =
                        existingReport.IsDoneOrNot,

                    ProgressPercentage =
                        existingReport.ProgressPercentage,

                    DateTime =
                        DateTime.Now,

                    UpdatedBy =
                        report.LastUpdatedBy
                };


            await _context.FollowUpReportUpdates
                .AddAsync(history);

            await _context.SaveChangesAsync();


            return existingReport;
        }


        // =========================================================
        // تحديث المهمة مع ملف
        // =========================================================

        public async Task<FollowUpReport> Update(
            FollowUpReport report,
            string newPath)
        {
            var existingReport =
                await _context.FollowUpReports
                    .FirstOrDefaultAsync(
                        x => x.Id == report.Id
                    );


            if (existingReport == null)
            {
                throw new Exception(
                    "المهمة غير موجودة");
            }


            // حذف الملف القديم إن وجد
            if (!string.IsNullOrWhiteSpace(
                    existingReport.Path))
            {
                await DeleteFile(
                    existingReport.Path);
            }


            existingReport.Path = newPath;

            existingReport.Content =
                report.Content;

            existingReport.TaskDetails =
                report.TaskDetails;

            existingReport.TaskType =
                report.TaskType;

            existingReport.ClientOrProject =
                report.ClientOrProject;

            existingReport.AssignedEmployee =
                report.AssignedEmployee;

            existingReport.Priority =
                report.Priority;

            existingReport.TotalItems =
                report.TotalItems;

            existingReport.CompletedItems =
                report.CompletedItems;

            existingReport.CompletedDetails =
                report.CompletedDetails;

            existingReport.ExpectedDurationDays =
                report.ExpectedDurationDays;

            existingReport.StartDate =
                report.StartDate;

            existingReport.DueDate =
                report.DueDate;

            existingReport.LastUpdatedDate =
                DateTime.Now;

            existingReport.LastUpdatedBy =
                report.LastUpdatedBy;


            SetTaskStatus(existingReport);


            await _context.SaveChangesAsync();


            return existingReport;
        }


        // =========================================================
        // إضافة تحديث للمهمة
        // =========================================================

        public async Task<FollowUpReportUpdate> AddUpdate(
            FollowUpReportUpdate update)
        {
            update.DateTime = DateTime.Now;


            await _context.FollowUpReportUpdates
                .AddAsync(update);


            await _context.SaveChangesAsync();


            return update;
        }


        // =========================================================
        // جلب سجل تحديثات مهمة
        // =========================================================

        public async Task<List<FollowUpReportUpdate>> GetUpdates(
            int reportId)
        {
            return await _context.FollowUpReportUpdates
                .Where(x =>
                    x.FollowUpReportId == reportId)
                .OrderByDescending(x => x.DateTime)
                .ToListAsync();
        }


        // =========================================================
        // حذف مهمة
        // =========================================================

        public async Task<bool> Delete(int Id)
        {
            try
            {
                var report =
                    await _context.FollowUpReports
                        .FirstOrDefaultAsync(
                            x => x.Id == Id
                        );


                if (report == null)
                    return false;


                // حذف الملف
                if (!string.IsNullOrWhiteSpace(report.Path))
                {
                    await DeleteFile(report.Path);
                }


                // حذف سجلات التحديثات
                var updates =
                    await _context.FollowUpReportUpdates
                        .Where(x =>
                            x.FollowUpReportId == Id)
                        .ToListAsync();


                if (updates.Any())
                {
                    _context.FollowUpReportUpdates
                        .RemoveRange(updates);
                }


                // حذف المهمة
                _context.FollowUpReports.Remove(report);


                await _context.SaveChangesAsync();


                return true;
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // حذف ملف
        // =========================================================

        public async Task<bool> DeleteFile(
            string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return true;


                // إذا كان المسار Web مثل:
                // /Files/example.jpg
                // نحاول تحويله لمسار فعلي
                var physicalPath = path;


                if (path.StartsWith("/"))
                {
                    physicalPath =
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            path.TrimStart('/')
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar)
                        );
                }


                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }


                return true;
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // إرسال تقرير يومي
        // =========================================================

        public async Task SendDailyReports(
            string email,
            DateTime date)
        {
            var todayReports =
                await _context.FollowUpReports
                    .Where(r =>
                        r.DateTime.Date == date.Date)
                    .OrderBy(r => r.DateTime)
                    .ToListAsync();


            if (!todayReports.Any())
                return;


            var reportContent =
                new StringBuilder();


            reportContent.Append(
                "<!DOCTYPE html>" +
                "<html>" +
                "<body " +
                "style='direction:rtl;" +
                "font-family:Arial;'>" +
                "<h2>تقرير متابعة المهام</h2>"
            );


            reportContent.Append(
                "<table " +
                "border='1' " +
                "cellpadding='8' " +
                "cellspacing='0' " +
                "style='border-collapse:collapse;" +
                "width:100%;'>"
            );


            reportContent.Append(
                "<thead>" +
                "<tr>" +
                "<th>الموظف</th>" +
                "<th>المهمة</th>" +
                "<th>النوع</th>" +
                "<th>المنجز</th>" +
                "<th>الإجمالي</th>" +
                "<th>النسبة</th>" +
                "<th>الحالة</th>" +
                "<th>التاريخ</th>" +
                "</tr>" +
                "</thead>"
            );


            reportContent.Append("<tbody>");


            foreach (var report in todayReports)
            {
                reportContent.AppendFormat(
                    "<tr>" +
                    "<td>{0}</td>" +
                    "<td>{1}</td>" +
                    "<td>{2}</td>" +
                    "<td>{3}</td>" +
                    "<td>{4}</td>" +
                    "<td>{5}%</td>" +
                    "<td>{6}</td>" +
                    "<td>{7}</td>" +
                    "</tr>",

                    report.FullName,

                    report.Content,

                    report.TaskType,

                    report.CompletedItems,

                    report.TotalItems,

                    report.ProgressPercentage,

                    report.IsDoneOrNot,

                    report.DateTime
                        .ToString("yyyy-MM-dd")
                );
            }


            reportContent.Append(
                "</tbody></table>"
            );


            reportContent.Append(
                "</body></html>"
            );


            using var mailMessage =
                new MailMessage();


            mailMessage.To.Add(email);

            mailMessage.Subject =
                "تقرير متابعة المهام";

            mailMessage.Body =
                reportContent.ToString();

            mailMessage.IsBodyHtml = true;


            foreach (var report in todayReports)
            {
                if (!string.IsNullOrWhiteSpace(report.Path))
                {
                    var physicalPath =
                        report.Path;


                    if (report.Path.StartsWith("/"))
                    {
                        physicalPath =
                            Path.Combine(
                                Directory.GetCurrentDirectory(),
                                "wwwroot",
                                report.Path
                                    .TrimStart('/')
                                    .Replace(
                                        '/',
                                        Path.DirectorySeparatorChar)
                            );
                    }


                    if (File.Exists(physicalPath))
                    {
                        mailMessage.Attachments.Add(
                            new Attachment(
                                physicalPath
                            )
                        );
                    }
                }
            }


            _email.SendEmail(
                email,
                "تقرير متابعة المهام",
                reportContent.ToString(),
                mailMessage.Attachments
            );
        }


        // =========================================================
        // الشكاوى الأسبوعية
        // =========================================================

        public Task SendWeeklyComplaint()
        {
            throw new NotImplementedException();
        }
    }
}