using System.Net;
using System.Net.Mail;

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

        public void SendEmail(
            string toEmail,
            string subject,
            string body,
            AttachmentCollection attachments)
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

                throw new Exception(
                    $"SMTP ERROR: {ex.Message} | Status: {ex.StatusCode} | Inner: {ex.InnerException?.Message}",
                    ex);
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
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "EMAIL ERROR");

                throw;
            }
        }

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
        public void TestGmail()
        {
            using var client = new SmtpClient(
                "smtp.gmail.com",
                587);

            client.EnableSsl = true;
            client.UseDefaultCredentials = false;

            client.Credentials = new NetworkCredential(
                _emailSender,
                _emailPassword);

            client.DeliveryMethod =
                SmtpDeliveryMethod.Network;

            client.Timeout = 30000;

            using var message = new MailMessage();

            message.From = new MailAddress(_emailSender);

            message.To.Add("hadi.nouman12345@gmail.com");

            message.Subject = "REMS SMTP TEST";

            message.Body = "<h1>SMTP Test</h1>";

            message.IsBodyHtml = true;

            client.Send(message);
        }
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
                System.Text.Encoding.UTF8;

            message.BodyEncoding =
                System.Text.Encoding.UTF8;

            return message;
        }
    }
}