using System.Net;
using System.Net.Http;
using System.Net.Mail;

namespace REMS.Services
{
    public class EmailService(IConfiguration configProvider)
    {
        private readonly string _smtpServer = "mail.rexos.co";
        private readonly int _smtpPort = 587;
        private readonly string _emailSender = configProvider["EmailSenderAddress"]!;
        private readonly string _emailPassword = configProvider["EmailSenderPassword"]!;

        public void SendEmail(string toEmail, string subject, string body)
        {
            using (var client = new SmtpClient(_smtpServer, _smtpPort))
            {
                client.EnableSsl = false;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSender, _emailPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 300000000;
                var mailMessage = new MailMessage(_emailSender, toEmail)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                
                client.Send(mailMessage);
            }
        }
        public void SendEmail(string toEmail, string subject, string body, AttachmentCollection attachments)
        {
            using (var client = new SmtpClient(_smtpServer, _smtpPort))
            {
                client.EnableSsl = false;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSender, _emailPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 300000000;

                var mailMessage = new MailMessage(_emailSender, toEmail)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                foreach (var attachment in attachments)
                {
                    mailMessage.Attachments.Add(attachment);
                }

                client.Send(mailMessage);
            }
        }
    }
}
