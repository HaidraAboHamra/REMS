using System.Net;
using System.Net.Mail;
using System.Text;
using REMS.Enititys;

namespace REMS.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _emailSender;
        private readonly string _emailPassword;

        public EmailService(
            IConfiguration config,
            ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;

            _smtpServer =
                _config["Email:SmtpServer"]
                ?? "smtp.gmail.com";

            _smtpPort =
                int.TryParse(
                    _config["Email:SmtpPort"],
                    out var port)
                    ? port
                    : 587;

            _emailSender =
                _config["EmailSenderAddress"]
                ?? throw new InvalidOperationException(
                    "EmailSenderAddress غير موجود.");

            _emailPassword =
                _config["EmailSenderPassword"]
                ?? throw new InvalidOperationException(
                    "EmailSenderPassword غير موجود.");
        }

        // =========================================================
        // إرسال بريد عادي
        // =========================================================

        public void SendEmail(
            string toEmail,
            string subject,
            string body,
            AttachmentCollection? attachments = null)
        {
            try
            {
                using var client = CreateClient();

                using var message =
                    CreateMessage(
                        toEmail,
                        subject,
                        body);

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        message.Attachments.Add(attachment);
                    }
                }

                _logger.LogInformation(
                    "Starting SMTP send. Server={Server}, Port={Port}, Sender={Sender}, Recipient={Recipient}, Attachments={Count}",
                    _smtpServer,
                    _smtpPort,
                    _emailSender,
                    toEmail,
                    message.Attachments.Count);

                client.Send(message);

                _logger.LogInformation(
                    "Email sent successfully to {Recipient}",
                    toEmail);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(
                    ex,
                    "SMTP ERROR. StatusCode={StatusCode}, Message={Message}, InnerException={InnerException}",
                    ex.StatusCode,
                    ex.Message,
                    ex.InnerException?.Message);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "EMAIL ERROR. Message={Message}, InnerException={InnerException}",
                    ex.Message,
                    ex.InnerException?.Message);

                throw;
            }
        }

        // =========================================================
        // إرسال بريد بدون مرفقات
        // =========================================================

        public void SendEmail(
            string toEmail,
            string subject,
            string body)
        {
            try
            {
                using var client = CreateClient();

                using var message =
                    CreateMessage(
                        toEmail,
                        subject,
                        body);

                client.Send(message);

                _logger.LogInformation(
                    "Email sent successfully to {Recipient}",
                    toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "EMAIL ERROR");

                throw;
            }
        }

        // =========================================================
        // إرسال Async
        // =========================================================

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            try
            {
                using var client = CreateClient();

                using var message =
                    CreateMessage(
                        toEmail,
                        subject,
                        body);

                await client.SendMailAsync(message);

                _logger.LogInformation(
                    "Async email sent successfully to {Recipient}",
                    toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ASYNC EMAIL ERROR");

                throw;
            }
        }

        // =========================================================
        // إرسال تفاصيل المهمة للموظف
        // =========================================================

        public async Task SendTaskAssignmentEmailAsync(
            FollowUpReport report,
            User assignedEmployee)
        {
            if (string.IsNullOrWhiteSpace(assignedEmployee.Email))
            {
                _logger.LogWarning(
                    "Assigned employee {EmployeeId} does not have an email address.",
                    assignedEmployee.Id);

                return;
            }

            string employeeName =
                HtmlEncode(
                    assignedEmployee.FullName ?? "الموظف");

            string creatorName =
                HtmlEncode(
                    report.FullName ?? "مدير قسم المتابعة");

            string taskTitle =
                HtmlEncode(
                    report.Content ?? "مهمة جديدة");

            string html =
                BuildTaskAssignmentHtml(
                    report,
                    employeeName,
                    creatorName);

            string subject =
                $"REMS | تم إسناد مهمة جديدة إليك | {taskTitle}";

            await SendEmailAsync(
                assignedEmployee.Email,
                subject,
                html);
        }

        // =========================================================
        // التقرير اليومي
        // =========================================================

        public async Task SendDailyTaskReportAsync(
            string toEmail,
            List<FollowUpReport> reports,
            DateTime reportDate)
        {
            reports ??=
                new List<FollowUpReport>();

            string html =
                BuildTaskReportHtml(
                    reports,
                    "التقرير اليومي للمهام",
                    reportDate,
                    reportDate);

            string subject =
                $"REMS | التقرير اليومي للمهام | {reportDate:yyyy-MM-dd}";

            await SendEmailAsync(
                toEmail,
                subject,
                html);
        }

        // =========================================================
        // التقرير الأسبوعي
        // =========================================================

        public async Task SendWeeklyTaskReportAsync(
            string toEmail,
            List<FollowUpReport> reports,
            DateTime weekStart,
            DateTime weekEnd)
        {
            reports ??=
                new List<FollowUpReport>();

            string html =
                BuildTaskReportHtml(
                    reports,
                    "التقرير الأسبوعي للمهام",
                    weekStart,
                    weekEnd);

            string subject =
                $"REMS | التقرير الأسبوعي للمهام | {weekStart:yyyy-MM-dd} - {weekEnd:yyyy-MM-dd}";

            await SendEmailAsync(
                toEmail,
                subject,
                html);
        }

        // =========================================================
        // إنشاء SMTP Client
        // =========================================================

        private SmtpClient CreateClient()
        {
            return new SmtpClient(
                _smtpServer,
                _smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,

                Credentials =
                    new NetworkCredential(
                        _emailSender,
                        _emailPassword),

                DeliveryMethod =
                    SmtpDeliveryMethod.Network,

                Timeout = 30000
            };
        }

        // =========================================================
        // إنشاء الرسالة
        // =========================================================

        private MailMessage CreateMessage(
            string toEmail,
            string subject,
            string body)
        {
            var message =
                new MailMessage();

            message.From =
                new MailAddress(
                    _emailSender);

            message.To.Add(
                new MailAddress(
                    toEmail));

            message.Subject =
                subject;

            message.Body =
                body;

            message.IsBodyHtml =
                true;

            message.SubjectEncoding =
                Encoding.UTF8;

            message.BodyEncoding =
                Encoding.UTF8;

            return message;
        }

        // =========================================================
        // HTML لتفاصيل المهمة
        // =========================================================

        private string BuildTaskAssignmentHtml(
            FollowUpReport report,
            string employeeName,
            string creatorName)
        {
            string taskDetails =
                HtmlEncode(
                    report.TaskDetails ?? "-");

            string taskType =
                HtmlEncode(
                    report.TaskType ?? "-");

            string client =
                HtmlEncode(
                    report.ClientOrProject ?? "-");

            string priority =
                HtmlEncode(
                    report.Priority ?? "-");

            string completedDetails =
                HtmlEncode(
                    report.CompletedDetails ?? "-");

            string taskTitle =
                HtmlEncode(
                    report.Content ?? "مهمة جديدة");

            string status =
                HtmlEncode(
                    report.IsDoneOrNot ?? "قيد التنفيذ");

            string startDate =
                report.StartDate.HasValue
                    ? report.StartDate.Value.ToString("yyyy-MM-dd")
                    : "-";

            string dueDate =
                report.DueDate.HasValue
                    ? report.DueDate.Value.ToString("yyyy-MM-dd")
                    : "-";

            int progress =
                Math.Clamp(
                    report.ProgressPercentage,
                    0,
                    100);

            return $@"
<!DOCTYPE html>

<html lang=""ar"" dir=""rtl"">

<head>

<meta charset=""UTF-8"">

<meta name=""viewport""
      content=""width=device-width, initial-scale=1.0"">

<title>
REMS - مهمة جديدة
</title>

<style>

* {{
    box-sizing:
        border-box;
}}

html,
body {{
    margin:
        0;

    padding:
        0;

    width:
        100%;

    min-height:
        100%;

    font-family:
        Arial,
        Tahoma,
        ""Segoe UI"",
        sans-serif;
}}

body {{
    background:
        #030305;

    color:
        #f8fafc;
}}

.email-wrapper {{
    width:
        100%;

    min-height:
        100vh;

    padding:
        28px 12px;

    position:
        relative;

    overflow:
        hidden;

    background:

        radial-gradient(
            circle at 82% 8%,
            rgba(67,56,202,.23),
            transparent 26%
        ),

        radial-gradient(
            circle at 15% 90%,
            rgba(29,78,216,.16),
            transparent 25%
        ),

        linear-gradient(
            135deg,
            #030305 0%,
            #070811 42%,
            #11102b 72%,
            #1e1b4b 100%
        );
}}

.email-wrapper::before {{
    content:
        """";

    position:
        absolute;

    inset:
        0;

    pointer-events:
        none;

    opacity:
        .28;

    background:

        linear-gradient(
            rgba(255,255,255,.025) 1px,
            transparent 1px
        ),

        linear-gradient(
            90deg,
            rgba(255,255,255,.025) 1px,
            transparent 1px
        );

    background-size:
        42px 42px;
}}

.email-wrapper::after {{
    content:
        """";

    position:
        absolute;

    top:
        0;

    left:
        -35%;

    width:
        35%;

    height:
        100%;

    transform:
        skewX(-22deg);

    background:
        linear-gradient(
            90deg,
            transparent,
            rgba(255,255,255,.055),
            transparent
        );

    pointer-events:
        none;

    animation:
        emailScan
        8s
        ease-in-out
        infinite;
}}

.container {{
    position:
        relative;

    z-index:
        2;

    width:
        100%;

    max-width:
        850px;

    margin:
        0 auto;

    overflow:
        hidden;

    border:
        1px solid
        rgba(129,140,248,.20);

    border-radius:
        28px;

    background:

        radial-gradient(
            circle at 85% 10%,
            rgba(129,140,248,.18),
            transparent 28%
        ),

        linear-gradient(
            135deg,
            rgba(3,3,5,.96),
            rgba(10,10,20,.95) 48%,
            rgba(23,20,73,.96) 100%
        );

    box-shadow:

        0 28px 90px
        rgba(0,0,0,.58),

        0 0 0 1px
        rgba(255,255,255,.02)
        inset,

        0 0 70px
        rgba(67,56,202,.12);
}}

.header {{
    position:
        relative;

    overflow:
        hidden;

    padding:
        40px 28px;

    text-align:
        center;

    border-bottom:
        1px solid
        rgba(129,140,248,.15);

    background:

        radial-gradient(
            circle at 50% -30%,
            rgba(99,102,241,.32),
            transparent 48%
        ),

        linear-gradient(
            135deg,
            rgba(49,46,129,.88),
            rgba(30,64,175,.58),
            rgba(7,8,17,.95)
        );
}}

.header::before {{
    content:
        """";

    position:
        absolute;

    width:
        420px;

    height:
        260px;

    top:
        -155px;

    right:
        50%;

    transform:
        translateX(50%);

    background:

        radial-gradient(
            circle,
            rgba(99,102,241,.28),
            transparent 65%
        );

    filter:
        blur(14px);
}}

.header-kicker {{
    position:
        relative;

    display:
        block;

    margin-bottom:
        12px;

    color:
        #a5b4fc;

    font-size:
        10px;

    font-weight:
        800;

    letter-spacing:
        .28em;

    text-transform:
        uppercase;
}}

.header-icon {{
    position:
        relative;

    width:
        76px;

    height:
        76px;

    margin:
        0 auto 18px;

    display:
        grid;

    place-items:
        center;

    border:
        1px solid
        rgba(255,255,255,.16);

    border-radius:
        26px;

    background:

        linear-gradient(
            145deg,
            rgba(99,102,241,.95),
            rgba(30,64,175,.75)
        );

    box-shadow:

        0 18px 42px
        rgba(79,70,229,.34),

        0 0 36px
        rgba(99,102,241,.22)
        inset,

        0 0 50px
        rgba(99,102,241,.16);

    animation:
        iconFloat
        4s
        ease-in-out
        infinite;
}}

.header-icon::before {{
    content:
        """";

    width:
        24px;

    height:
        24px;

    border-radius:
        50%;

    background:
        #f8fafc;

    box-shadow:

        0 0 16px
        rgba(255,255,255,.72),

        0 0 36px
        rgba(129,140,248,.62);
}}

.header h1 {{
    position:
        relative;

    margin:
        0;

    color:
        #f8fafc;

    font-size:
        30px;

    font-weight:
        700;

    line-height:
        1.3;

    letter-spacing:
        -.04em;

    text-shadow:

        0 0 18px
        rgba(129,140,248,.12);
}}

.header p {{
    position:
        relative;

    margin:
        10px 0 0;

    color:
        rgba(226,232,240,.68);

    font-size:
        14px;

    line-height:
        1.8;
}}

.content {{
    position:
        relative;

    padding:
        30px;
}}

.greeting {{
    margin-bottom:
        22px;

    padding:
        20px 22px;

    border:
        1px solid
        rgba(129,140,248,.14);

    border-radius:
        18px;

    background:

        linear-gradient(
            135deg,
            rgba(255,255,255,.045),
            rgba(99,102,241,.05)
        );

    box-shadow:
        0 12px 35px
        rgba(0,0,0,.18);

    color:
        rgba(226,232,240,.78);

    font-size:
        15px;

    line-height:
        1.9;
}}

.greeting strong {{
    color:
        #c7d2fe;
}}

.task-title-box {{
    position:
        relative;

    overflow:
        hidden;

    margin-bottom:
        22px;

    padding:
        19px 20px;

    border:
        1px solid
        rgba(99,102,241,.18);

    border-radius:
        18px;

    background:

        radial-gradient(
            circle at 88% 0%,
            rgba(99,102,241,.14),
            transparent 34%
        ),

        linear-gradient(
            135deg,
            rgba(67,56,202,.10),
            rgba(30,64,175,.06)
        );
}}

.task-title-box::after {{
    content:
        """";

    position:
        absolute;

    right:
        0;

    top:
        0;

    width:
        3px;

    height:
        100%;

    background:

        linear-gradient(
            180deg,
            #6366f1,
            #1d4ed8
        );

    box-shadow:
        0 0 16px
        rgba(99,102,241,.55);
}}

.task-title-label {{
    display:
        block;

    margin-bottom:
        6px;

    color:
        #818cf8;

    font-size:
        10px;

    font-weight:
        800;

    letter-spacing:
        .18em;

    text-transform:
        uppercase;
}}

.task-title {{
    color:
        #f8fafc;

    font-size:
        20px;

    font-weight:
        800;

    line-height:
        1.6;
}}

.info-table {{
    width:
        100%;

    border-collapse:
        separate;

    border-spacing:
        0 7px;
}}

.info-table td {{
    padding:
        12px 14px;

    border:
        1px solid
        rgba(129,140,248,.07);

    background:
        rgba(255,255,255,.025);

    color:
        rgba(241,245,249,.82);

    vertical-align:
        top;

    line-height:
        1.7;
}}

.info-table tr td:first-child {{
    width:
        185px;

    border-radius:
        12px 0 0 12px;

    background:
        rgba(79,70,229,.07);

    color:
        #a5b4fc;

    font-weight:
        700;
}}

.info-table tr td:last-child {{
    border-radius:
        0 12px 12px 0;
}}

.status-pill {{
    display:
        inline-block;

    padding:
        5px 12px;

    border:
        1px solid
        rgba(129,140,248,.20);

    border-radius:
        999px;

    background:
        rgba(99,102,241,.10);

    color:
        #c7d2fe;

    font-size:
        12px;

    font-weight:
        800;
}}

.progress-box {{
    margin-top:
        26px;

    padding:
        20px;

    border:
        1px solid
        rgba(129,140,248,.13);

    border-radius:
        18px;

    background:

        radial-gradient(
            circle at 85% 50%,
            rgba(99,102,241,.10),
            transparent 35%
        ),

        rgba(255,255,255,.025);
}}

.progress-top {{
    display:
        flex;

    align-items:
        center;

    justify-content:
        space-between;

    gap:
        10px;

    margin-bottom:
        10px;
}}

.progress-title {{
    color:
        #cbd5e1;

    font-size:
        14px;

    font-weight:
        700;
}}

.progress-value {{
    color:
        #818cf8;

    font-size:
        15px;

    font-weight:
        900;

    text-shadow:
        0 0 14px
        rgba(99,102,241,.35);
}}

.progress-container {{
    width:
        100%;

    height:
        12px;

    overflow:
        hidden;

    border-radius:
        999px;

    background:
        rgba(255,255,255,.06);

    box-shadow:
        inset
        0 1px 2px
        rgba(0,0,0,.28);
}}

.progress {{
    height:
        100%;

    width:
        {progress}%;

    border-radius:
        999px;

    background:

        linear-gradient(
            90deg,
            #312e81,
            #4338ca 55%,
            #6366f1
        );

    box-shadow:
        0 0 18px
        rgba(99,102,241,.38);
}}

.footer {{
    position:
        relative;

    padding:
        22px;

    text-align:
        center;

    border-top:
        1px solid
        rgba(129,140,248,.10);

    background:
        rgba(255,255,255,.025);

    color:
        rgba(148,163,184,.68);

    font-size:
        11px;

    line-height:
        1.8;
}}

.footer-brand {{
    color:
        #818cf8;

    font-weight:
        800;
}}

.footer-date {{
    margin-top:
        4px;

    color:
        rgba(148,163,184,.50);
}}

@keyframes emailScan {{

    0%,18% {{
        left:
            -35%;

        opacity:
            0;
    }}

    35% {{
        opacity:
            1;
    }}

    58%,100% {{
        left:
            115%;

        opacity:
            0;
    }}
}}

@keyframes iconFloat {{

    0%,100% {{
        transform:
            translateY(0)
            rotate(0deg);
    }}

    50% {{
        transform:
            translateY(-7px)
            rotate(2deg);
    }}
}}

@media only screen and (max-width:640px) {{

    .email-wrapper {{
        padding:
            12px 6px;
    }}

    .container {{
        border-radius:
            20px;
    }}

    .header {{
        padding:
            32px 18px;
    }}

    .header h1 {{
        font-size:
            24px;
    }}

    .content {{
        padding:
            20px 14px;
    }}

    .greeting {{
        padding:
            16px;
    }}

    .task-title-box {{
        padding:
            16px;
    }}

    .task-title {{
        font-size:
            18px;
    }}

    .info-table,
    .info-table tbody,
    .info-table tr,
    .info-table td {{
        display:
            block;

        width:
            100% !important;
    }}

    .info-table tr {{
        margin-bottom:
            7px;
    }}

    .info-table tr td:first-child {{
        border-radius:
            12px 12px 0 0;
    }}

    .info-table tr td:last-child {{
        border-radius:
            0 0 12px 12px;
    }}
}}

</style>

</head>

<body>

<div class=""email-wrapper"">

<div class=""container"">

<div class=""header"">

<span class=""header-kicker"">
REMS • HEX STUDIO
</span>

<div class=""header-icon""></div>

<h1>
مهمة جديدة
</h1>

<p>
نظام REMS لإدارة ومتابعة الأعمال والمهام
</p>

</div>

<div class=""content"">

<div class=""greeting"">

مرحباً
<strong>
{employeeName}
</strong>

<br />

تم إسناد المهمة التالية إليك من قبل
<strong>
{creatorName}
</strong>.

</div>

<div class=""task-title-box"">

<span class=""task-title-label"">
TASK
</span>

<div class=""task-title"">
{taskTitle}
</div>

</div>

<table class=""info-table"">

<tr>

<td>
تفاصيل المهمة
</td>

<td>
{taskDetails}
</td>

</tr>

<tr>

<td>
نوع المهمة
</td>

<td>
{taskType}
</td>

</tr>

<tr>

<td>
العميل / المشروع
</td>

<td>
{client}
</td>

</tr>

<tr>

<td>
الأولوية
</td>

<td>
{priority}
</td>

</tr>

<tr>

<td>
عدد البنود
</td>

<td>
{report.TotalItems}
</td>

</tr>

<tr>

<td>
المنجز
</td>

<td>
{report.CompletedItems}
</td>

</tr>

<tr>

<td>
المتبقي
</td>

<td>
{report.RemainingItems}
</td>

</tr>

<tr>

<td>
الحالة
</td>

<td>

<span class=""status-pill"">
{status}
</span>

</td>

</tr>

<tr>

<td>
تاريخ البدء
</td>

<td>
{startDate}
</td>

</tr>

<tr>

<td>
تاريخ التسليم
</td>

<td>
{dueDate}
</td>

</tr>

<tr>

<td>
ما تم إنجازه
</td>

<td>
{completedDetails}
</td>

</tr>

</table>

<div class=""progress-box"">

<div class=""progress-top"">

<div class=""progress-title"">
نسبة التقدم
</div>

<div class=""progress-value"">
{progress}%
</div>

</div>

<div class=""progress-container"">

<div class=""progress""></div>

</div>

</div>

</div>

<div class=""footer"">

تم إرسال هذه الرسالة تلقائياً من
<span class=""footer-brand"">
REMS
</span>

<br />

<div class=""footer-date"">

تاريخ الإرسال:
{DateTime.Now:yyyy-MM-dd HH:mm}

</div>

</div>

</div>

</div>

</body>

</html>";
        }

        // =========================================================
        // HTML للتقرير اليومي والأسبوعي
        // =========================================================

        private string BuildTaskReportHtml(
            List<FollowUpReport> reports,
            string reportTitle,
            DateTime periodStart,
            DateTime periodEnd)
        {
            int totalTasks =
                reports.Count;

            int completedTasks =
                reports.Count(x =>
                    x.IsDone ||
                    x.CompletedItems >= x.TotalItems);

            int pendingTasks =
                reports.Count -
                completedTasks;

            int overdueTasks =
                reports.Count(x =>
                    x.DueDate.HasValue &&
                    x.DueDate.Value.Date < DateTime.Today &&
                    !x.IsDone);

            int totalItems =
                reports.Sum(x =>
                    x.TotalItems);

            int completedItems =
                reports.Sum(x =>
                    x.CompletedItems);

            int remainingItems =
                reports.Sum(x =>
                    x.RemainingItems);

            int overallProgress =
                totalItems > 0
                    ? (int)Math.Round(
                        ((double)completedItems /
                         totalItems) * 100)
                    : 0;

            overallProgress =
                Math.Clamp(
                    overallProgress,
                    0,
                    100);

            var html =
                new StringBuilder();

            html.Append($@"
<!DOCTYPE html>

<html lang=""ar"" dir=""rtl"">

<head>

<meta charset=""UTF-8"">

<meta name=""viewport""
      content=""width=device-width, initial-scale=1.0"">

<title>
{HtmlEncode(reportTitle)}
</title>

<style>

* {{
    box-sizing:
        border-box;
}}

html,
body {{
    margin:
        0;

    padding:
        0;

    width:
        100%;

    min-height:
        100%;

    font-family:
        Arial,
        Tahoma,
        ""Segoe UI"",
        sans-serif;
}}

body {{
    background:
        #030305;

    color:
        #f8fafc;
}}

.email-wrapper {{
    width:
        100%;

    min-height:
        100vh;

    padding:
        28px 12px;

    position:
        relative;

    overflow:
        hidden;

    background:

        radial-gradient(
            circle at 82% 8%,
            rgba(67,56,202,.23),
            transparent 26%
        ),

        radial-gradient(
            circle at 15% 90%,
            rgba(29,78,216,.16),
            transparent 25%
        ),

        linear-gradient(
            135deg,
            #030305 0%,
            #070811 42%,
            #11102b 72%,
            #1e1b4b 100%
        );
}}

.email-wrapper::before {{
    content:
        """";

    position:
        absolute;

    inset:
        0;

    pointer-events:
        none;

    opacity:
        .28;

    background:

        linear-gradient(
            rgba(255,255,255,.025) 1px,
            transparent 1px
        ),

        linear-gradient(
            90deg,
            rgba(255,255,255,.025) 1px,
            transparent 1px
        );

    background-size:
        42px 42px;
}}

.email-wrapper::after {{
    content:
        """";

    position:
        absolute;

    top:
        0;

    left:
        -35%;

    width:
        35%;

    height:
        100%;

    transform:
        skewX(-22deg);

    background:
        linear-gradient(
            90deg,
            transparent,
            rgba(255,255,255,.055),
            transparent
        );

    pointer-events:
        none;

    animation:
        reportScan
        8s
        ease-in-out
        infinite;
}}

.container {{
    position:
        relative;

    z-index:
        2;

    width:
        100%;

    max-width:
        1100px;

    margin:
        0 auto;

    overflow:
        hidden;

    border:
        1px solid
        rgba(129,140,248,.18);

    border-radius:
        28px;

    background:

        radial-gradient(
            circle at 85% 8%,
            rgba(99,102,241,.11),
            transparent 25%
        ),

        linear-gradient(
            135deg,
            rgba(3,3,5,.97),
            rgba(10,10,20,.96) 48%,
            rgba(23,20,73,.97) 100%
        );

    box-shadow:

        0 30px 100px
        rgba(0,0,0,.58),

        0 0 70px
        rgba(67,56,202,.10);
}}

.header {{
    position:
        relative;

    overflow:
        hidden;

    padding:
        42px 30px;

    text-align:
        center;

    border-bottom:
        1px solid
        rgba(129,140,248,.14);

    background:

        radial-gradient(
            circle at 50% -40%,
            rgba(99,102,241,.34),
            transparent 50%
        ),

        linear-gradient(
            135deg,
            rgba(49,46,129,.82),
            rgba(30,64,175,.55),
            rgba(7,8,17,.94)
        );
}}

.header::before {{
    content:
        """";

    position:
        absolute;

    width:
        400px;

    height:
        250px;

    top:
        -155px;

    right:
        50%;

    transform:
        translateX(50%);

    background:

        radial-gradient(
            circle,
            rgba(99,102,241,.30),
            transparent 68%
        );

    filter:
        blur(14px);
}}

.header-kicker {{
    position:
        relative;

    display:
        block;

    margin-bottom:
        9px;

    color:
        #a5b4fc;

    font-size:
        10px;

    font-weight:
        800;

    letter-spacing:
        .28em;

    text-transform:
        uppercase;
}}

.header-icon {{
    position:
        relative;

    width:
        72px;

    height:
        72px;

    margin:
        0 auto 16px;

    display:
        grid;

    place-items:
        center;

    border:
        1px solid
        rgba(255,255,255,.14);

    border-radius:
        24px;

    background:

        linear-gradient(
            145deg,
            rgba(99,102,241,.94),
            rgba(30,64,175,.72)
        );

    box-shadow:

        0 18px 42px
        rgba(79,70,229,.32),

        0 0 36px
        rgba(99,102,241,.20)
        inset;
}}

.header-icon::before {{
    content:
        """";

    width:
        22px;

    height:
        22px;

    border-radius:
        50%;

    background:
        #f8fafc;

    box-shadow:

        0 0 14px
        rgba(255,255,255,.68),

        0 0 38px
        rgba(129,140,248,.58);
}}

.header h1 {{
    position:
        relative;

    margin:
        0;

    color:
        #f8fafc;

    font-size:
        30px;

    font-weight:
        700;

    line-height:
        1.3;

    letter-spacing:
        -.04em;

    text-shadow:

        0 0 18px
        rgba(129,140,248,.12);
}}

.header p {{
    position:
        relative;

    margin:
        10px 0 0;

    color:
        rgba(226,232,240,.66);

    font-size:
        14px;

    line-height:
        1.8;
}}

.content {{
    padding:
        28px;
}}

.stats {{
    display:
        table;

    width:
        100%;

    table-layout:
        fixed;

    margin-bottom:
        24px;
}}

.stat {{
    display:
        table-cell;

    padding:
        6px;
}}

.stat-box {{
    position:
        relative;

    overflow:
        hidden;

    padding:
        20px 8px;

    text-align:
        center;

    border:
        1px solid
        rgba(129,140,248,.12);

    border-radius:
        18px;

    background:

        radial-gradient(
            circle at 50% 0%,
            rgba(99,102,241,.11),
            transparent 55%
        ),

        rgba(255,255,255,.025);

    box-shadow:
        0 12px 35px
        rgba(0,0,0,.20);
}}

.stat-box::after {{
    content:
        """";

    position:
        absolute;

    left:
        20%;

    right:
        20%;

    bottom:
        0;

    height:
        1px;

    background:

        linear-gradient(
            90deg,
            transparent,
            rgba(99,102,241,.62),
            transparent
        );
}}

.stat-number {{
    display:
        block;

    color:
        #f8fafc;

    font-size:
        28px;

    font-weight:
        800;

    line-height:
        1;
}}

.stat-label {{
    display:
        block;

    margin-top:
        8px;

    color:
        rgba(165,180,252,.78);

    font-size:
        12px;
}}

.section {{
    margin-top:
        26px;
}}

.section-title {{
    position:
        relative;

    margin:
        0 0 13px;

    color:
        #c7d2fe;

    font-size:
        19px;

    font-weight:
        800;
}}

.section-title::before {{
    content:
        """";

    display:
        inline-block;

    width:
        4px;

    height:
        18px;

    margin-left:
        9px;

    vertical-align:
        -3px;

    border-radius:
        10px;

    background:

        linear-gradient(
            180deg,
            #6366f1,
            #1d4ed8
        );

    box-shadow:
        0 0 12px
        rgba(99,102,241,.55);
}}

.progress-card {{
    padding:
        20px;

    border:
        1px solid
        rgba(129,140,248,.12);

    border-radius:
        18px;

    background:

        radial-gradient(
            circle at 80% 0%,
            rgba(99,102,241,.11),
            transparent 38%
        ),

        rgba(255,255,255,.025);
}}

.progress-top {{
    display:
        flex;

    align-items:
        center;

    justify-content:
        space-between;

    margin-bottom:
        10px;
}}

.progress-label {{
    color:
        #cbd5e1;

    font-size:
        14px;

    font-weight:
        700;
}}

.progress-number {{
    color:
        #818cf8;

    font-size:
        15px;

    font-weight:
        900;

    text-shadow:
        0 0 14px
        rgba(99,102,241,.32);
}}

.progress-container {{
    width:
        100%;

    height:
        12px;

    overflow:
        hidden;

    border-radius:
        999px;

    background:
        rgba(255,255,255,.055);

    box-shadow:
        inset
        0 1px 2px
        rgba(0,0,0,.30);
}}

.progress {{
    height:
        100%;

    width:
        {overallProgress}%;

    border-radius:
        999px;

    background:

        linear-gradient(
            90deg,
            #312e81,
            #4338ca 50%,
            #6366f1
        );

    box-shadow:
        0 0 18px
        rgba(99,102,241,.36);
}}

.summary-table {{
    width:
        100%;

    border-collapse:
        separate;

    border-spacing:
        0 7px;
}}

.summary-table td {{
    padding:
        12px 14px;

    border:
        1px solid
        rgba(129,140,248,.07);

    background:
        rgba(255,255,255,.025);

    color:
        rgba(241,245,249,.82);
}}

.summary-table td:first-child {{
    width:
        220px;

    border-radius:
        12px 0 0 12px;

    background:
        rgba(79,70,229,.055);

    color:
        #a5b4fc;

    font-weight:
        700;
}}

.summary-table td:last-child {{
    border-radius:
        0 12px 12px 0;
}}

.task {{
    position:
        relative;

    overflow:
        hidden;

    margin-bottom:
        18px;

    border:
        1px solid
        rgba(129,140,248,.12);

    border-radius:
        20px;

    background:

        linear-gradient(
            135deg,
            rgba(255,255,255,.035),
            rgba(99,102,241,.035)
        );

    box-shadow:
        0 15px 45px
        rgba(0,0,0,.22);
}}

.task::before {{
    content:
        """";

    position:
        absolute;

    top:
        0;

    bottom:
        0;

    right:
        0;

    width:
        3px;

    background:

        linear-gradient(
            180deg,
            #6366f1,
            #1d4ed8
        );

    box-shadow:
        0 0 16px
        rgba(99,102,241,.45);
}}

.task-header {{
    padding:
        18px 20px;

    border-bottom:
        1px solid
        rgba(129,140,248,.08);

    background:

        radial-gradient(
            circle at 95% 0%,
            rgba(99,102,241,.10),
            transparent 30%
        ),

        rgba(255,255,255,.02);
}}

.task-kicker {{
    margin-bottom:
        6px;

    color:
        #818cf8;

    font-size:
        10px;

    font-weight:
        800;

    letter-spacing:
        .14em;

    text-transform:
        uppercase;
}}

.task-title {{
    color:
        #f8fafc;

    font-size:
        18px;

    font-weight:
        800;

    line-height:
        1.6;
}}

.task-body {{
    padding:
        18px 20px;
}}

.info-table {{
    width:
        100%;

    border-collapse:
        separate;

    border-spacing:
        0 6px;
}}

.info-table td {{
    padding:
        10px 12px;

    border:
        1px solid
        rgba(129,140,248,.06);

    background:
        rgba(255,255,255,.018);

    color:
        rgba(241,245,249,.80);

    line-height:
        1.7;
}}

.info-table td:first-child {{
    width:
        175px;

    border-radius:
        10px 0 0 10px;

    background:
        rgba(79,70,229,.055);

    color:
        #a5b4fc;

    font-weight:
        700;
}}

.info-table td:last-child {{
    border-radius:
        0 10px 10px 0;
}}

.empty {{
    padding:
        45px 20px;

    text-align:
        center;

    border:
        1px solid
        rgba(129,140,248,.10);

    border-radius:
        18px;

    background:
        rgba(255,255,255,.02);

    color:
        rgba(148,163,184,.72);
}}

.footer {{
    padding:
        22px;

    text-align:
        center;

    border-top:
        1px solid
        rgba(129,140,248,.10);

    background:
        rgba(255,255,255,.02);

    color:
        rgba(148,163,184,.65);

    font-size:
        11px;

    line-height:
        1.8;
}}

.footer-brand {{
    color:
        #818cf8;

    font-weight:
        800;
}}

.footer-date {{
    margin-top:
        5px;

    color:
        rgba(148,163,184,.48);
}}

@keyframes reportScan {{

    0%,18% {{
        left:
            -35%;

        opacity:
            0;
    }}

    35% {{
        opacity:
            1;
    }}

    58%,100% {{
        left:
            115%;

        opacity:
            0;
    }}
}}

@media only screen and (max-width:700px) {{

    .email-wrapper {{
        padding:
            12px 6px;
    }}

    .container {{
        border-radius:
            20px;
    }}

    .header {{
        padding:
            30px 18px;
    }}

    .header h1 {{
        font-size:
            24px;
    }}

    .content {{
        padding:
            18px 12px;
    }}

    .stats {{
        display:
            block;
    }}

    .stat {{
        display:
            block;

        width:
            100%;

        padding:
            4px 0;
    }}

    .summary-table,
    .summary-table tbody,
    .summary-table tr,
    .summary-table td,
    .info-table,
    .info-table tbody,
    .info-table tr,
    .info-table td {{
        display:
            block;

        width:
            100% !important;
    }}

    .summary-table tr,
    .info-table tr {{
        margin-bottom:
            7px;
    }}

    .summary-table td:first-child,
    .info-table td:first-child {{
        border-radius:
            10px 10px 0 0;
    }}

    .summary-table td:last-child,
    .info-table td:last-child {{
        border-radius:
            0 0 10px 10px;
    }}
}}

</style>

</head>

<body>

<div class=""email-wrapper"">

<div class=""container"">

<div class=""header"">

<span class=""header-kicker"">
REMS • HEX STUDIO
</span>

<div class=""header-icon""></div>

<h1>
{HtmlEncode(reportTitle)}
</h1>

<p>
من {periodStart:yyyy-MM-dd}
إلى {periodEnd:yyyy-MM-dd}
</p>

</div>

<div class=""content"">

<div class=""stats"">

<div class=""stat"">

<div class=""stat-box"">

<span class=""stat-number"">
{totalTasks}
</span>

<span class=""stat-label"">
إجمالي المهام
</span>

</div>

</div>

<div class=""stat"">

<div class=""stat-box"">

<span class=""stat-number"">
{completedTasks}
</span>

<span class=""stat-label"">
مكتملة
</span>

</div>

</div>

<div class=""stat"">

<div class=""stat-box"">

<span class=""stat-number"">
{pendingTasks}
</span>

<span class=""stat-label"">
قيد التنفيذ
</span>

</div>

</div>

<div class=""stat"">

<div class=""stat-box"">

<span class=""stat-number"">
{overdueTasks}
</span>

<span class=""stat-label"">
متأخرة
</span>

</div>

</div>

</div>

<div class=""section"">

<div class=""section-title"">
نسبة الإنجاز العامة
</div>

<div class=""progress-card"">

<div class=""progress-top"">

<div class=""progress-label"">
التقدم الكلي للمهام
</div>

<div class=""progress-number"">
{overallProgress}%
</div>

</div>

<div class=""progress-container"">

<div class=""progress""></div>

</div>

</div>

</div>

<div class=""section"">

<div class=""section-title"">
ملخص البنود
</div>

<table class=""summary-table"">

<tr>

<td>
إجمالي البنود
</td>

<td>
{totalItems}
</td>

</tr>

<tr>

<td>
البنود المنجزة
</td>

<td>
{completedItems}
</td>

</tr>

<tr>

<td>
البنود المتبقية
</td>

<td>
{remainingItems}
</td>

</tr>

</table>

</div>

<div class=""section"">

<div class=""section-title"">
تفاصيل المهام
</div>
");

            foreach (
                var report in reports
                    .OrderBy(x => x.DueDate)
                    .ThenBy(x => x.Priority))
            {
                string status =
                    report.IsDone ||
                    report.CompletedItems >= report.TotalItems
                        ? "مكتملة"
                        : !string.IsNullOrWhiteSpace(
                            report.IsDoneOrNot)
                            ? report.IsDoneOrNot!
                            : "قيد التنفيذ";

                bool overdue =
                    report.DueDate.HasValue &&
                    report.DueDate.Value.Date < DateTime.Today &&
                    !report.IsDone;

                if (overdue)
                {
                    status = "متأخرة";
                }

                int taskProgress =
                    Math.Clamp(
                        report.ProgressPercentage,
                        0,
                        100);

                html.Append($@"

<div class=""task"">

<div class=""task-header"">

<div class=""task-kicker"">
TASK
</div>

<div class=""task-title"">
{HtmlEncode(
    report.Content ??
    "مهمة بدون عنوان")}
</div>

</div>

<div class=""task-body"">

<table class=""info-table"">

<tr>

<td>
النوع
</td>

<td>
{HtmlEncode(report.TaskType)}
</td>

</tr>

<tr>

<td>
العميل / المشروع
</td>

<td>
{HtmlEncode(report.ClientOrProject)}
</td>

</tr>

<tr>

<td>
الموظف المسؤول
</td>

<td>
{HtmlEncode(report.AssignedEmployee)}
</td>

</tr>

<tr>

<td>
الأولوية
</td>

<td>
{HtmlEncode(report.Priority)}
</td>

</tr>

<tr>

<td>
التقدم
</td>

<td>
<strong>
{taskProgress}%
</strong>
</td>

</tr>

<tr>

<td>
المنجز
</td>

<td>
{report.CompletedItems}
/
{report.TotalItems}
</td>

</tr>

<tr>

<td>
المتبقي
</td>

<td>
{report.RemainingItems}
</td>

</tr>

<tr>

<td>
الحالة
</td>

<td>
{HtmlEncode(status)}
</td>

</tr>

<tr>

<td>
التسليم
</td>

<td>
{(
    report.DueDate.HasValue
        ? report.DueDate.Value.ToString("yyyy-MM-dd")
        : "-"
)}
</td>

</tr>

</table>

</div>

</div>
");
            }

            if (!reports.Any())
            {
                html.Append(@"

<div class=""empty"">

لا توجد مهام ضمن الفترة المحددة.

</div>
");
            }

            html.Append($@"

</div>

</div>

<div class=""footer"">

تم إنشاء هذا التقرير تلقائياً بواسطة نظام

<span class=""footer-brand"">
REMS
</span>

<div class=""footer-date"">

تاريخ إنشاء التقرير:
{DateTime.Now:yyyy-MM-dd HH:mm}

</div>

</div>

</div>

</div>

</body>

</html>
");

            return html.ToString();
        }

        // =========================================================
        // حماية النصوص داخل HTML
        // =========================================================

        private static string HtmlEncode(
            string? value)
        {
            return WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(value)
                    ? "-"
                    : value);
        }

        // =========================================================
        // اختبار Gmail
        // =========================================================

        public void TestGmail()
        {
            using var client =
                new SmtpClient(
                    "smtp.gmail.com",
                    587);

            client.EnableSsl = true;
            client.UseDefaultCredentials = false;

            client.Credentials =
                new NetworkCredential(
                    _emailSender,
                    _emailPassword);

            client.DeliveryMethod =
                SmtpDeliveryMethod.Network;

            client.Timeout =
                30000;

            using var message =
                new MailMessage();

            message.From =
                new MailAddress(
                    _emailSender);

            message.To.Add(
                "hadi.nouman12345@gmail.com");

            message.Subject =
                "REMS SMTP TEST";

            message.Body =
                "<h1>SMTP Test</h1>";

            message.IsBodyHtml =
                true;

            client.Send(message);
        }
    }
}