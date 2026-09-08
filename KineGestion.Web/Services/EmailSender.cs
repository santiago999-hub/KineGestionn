using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace KineGestion.Web.Services
{
    public sealed class EmailEnvelope
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    public interface IEmailSender
    {
        bool IsConfigured { get; }
        Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken = default);
    }

    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsConfigured
            => !string.IsNullOrWhiteSpace(_configuration["Reminders:Email:SmtpHost"])
                && !string.IsNullOrWhiteSpace(_configuration["Reminders:Email:From"]);

        public async Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(envelope.To))
                throw new InvalidOperationException("Envelope sin destinatario.");

            var host = _configuration["Reminders:Email:SmtpHost"];
            var from = _configuration["Reminders:Email:From"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                throw new InvalidOperationException("Canal email habilitado, pero falta configuración SmtpHost o From.");
            var port = OperationalConfig.ReadBoundedInt(
                _configuration,
                _logger,
                "Reminders:Email:SmtpPort",
                defaultValue: 587,
                min: 1,
                max: 65535);
            var timeoutSeconds = OperationalConfig.ReadBoundedInt(
                _configuration,
                _logger,
                "Reminders:Email:TimeoutSeconds",
                defaultValue: 15,
                min: 3,
                max: 120);
            var user = _configuration["Reminders:Email:Username"];
            var pass = _configuration["Reminders:Email:Password"];
            var useSsl = _configuration.GetValue<bool?>("Reminders:Email:EnableSsl") ?? true;

            cancellationToken.ThrowIfCancellationRequested();

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = timeoutSeconds * 1000
            };

            if (!string.IsNullOrWhiteSpace(user))
                smtp.Credentials = new NetworkCredential(user, pass ?? string.Empty);

            using var mail = new MailMessage(from, envelope.To)
            {
                Subject = envelope.Subject,
                Body = envelope.Body,
                IsBodyHtml = false
            };

            await smtp.SendMailAsync(mail);
        }
    }
}