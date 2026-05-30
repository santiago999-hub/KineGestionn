using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;

namespace KineGestion.Web.Services
{
    public interface IReminderDeliveryService
    {
        Task<ReminderDeliveryResult> SendAsync(ReminderDeliveryRequest request, CancellationToken cancellationToken = default);
        ReminderPreviewResult BuildPreview(ReminderDeliveryRequest request);
    }

    public class ReminderDeliveryRequest
    {
        public int SessionId { get; set; }
        public DateTime FechaHora { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string? PacienteEmail { get; set; }
        public string? PacienteTelefono { get; set; }
        public string ProfesionalNombre { get; set; } = string.Empty;
        public string? TratamientoDescripcion { get; set; }
        public string ConfirmUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class ReminderDeliveryResult
    {
        public bool EmailSent { get; set; }
        public bool WhatsAppSent { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool AnyChannelSent => EmailSent || WhatsAppSent;
    }

    public class ReminderPreviewResult
    {
        public string EmailSubject { get; set; } = string.Empty;
        public string EmailBody { get; set; } = string.Empty;
        public string WhatsAppBody { get; set; } = string.Empty;
        public bool CanEmail { get; set; }
        public bool CanWhatsApp { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class ReminderDeliveryService : IReminderDeliveryService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ReminderDeliveryService> _logger;

        public ReminderDeliveryService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<ReminderDeliveryService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ReminderDeliveryResult> SendAsync(ReminderDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            var result = new ReminderDeliveryResult();

            var emailEnabled = _configuration.GetValue<bool>("Reminders:Email:Enabled");
            var whatsappEnabled = _configuration.GetValue<bool>("Reminders:WhatsApp:Enabled");

            if (!emailEnabled && !whatsappEnabled)
            {
                result.Errors.Add("Ningún canal de envío está habilitado en configuración.");
                return result;
            }

            var emailSubject = BuildEmailSubject(request);
            var emailBody = BuildEmailBody(request);
            var whatsappBody = BuildWhatsAppBody(request);

            if (emailEnabled)
                await TrySendEmailAsync(request, emailSubject, emailBody, result, cancellationToken);

            if (whatsappEnabled)
                await TrySendWhatsAppAsync(request, whatsappBody, result, cancellationToken);

            if (!result.AnyChannelSent && result.Errors.Count == 0)
                result.Errors.Add("No se pudo enviar: faltan datos de contacto válidos.");

            return result;
        }

        public ReminderPreviewResult BuildPreview(ReminderDeliveryRequest request)
        {
            var preview = new ReminderPreviewResult
            {
                EmailSubject = BuildEmailSubject(request),
                EmailBody = BuildEmailBody(request),
                WhatsAppBody = BuildWhatsAppBody(request),
                CanEmail = !string.IsNullOrWhiteSpace(request.PacienteEmail),
                CanWhatsApp = !string.IsNullOrWhiteSpace(request.PacienteTelefono)
            };

            if (!preview.CanEmail)
                preview.Warnings.Add("Sin email de destino para el canal Email.");

            if (!preview.CanWhatsApp)
                preview.Warnings.Add("Sin teléfono de destino para el canal WhatsApp.");

            return preview;
        }

        private async Task TrySendEmailAsync(ReminderDeliveryRequest request, string subject, string body, ReminderDeliveryResult result, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PacienteEmail))
            {
                result.Errors.Add($"Sesión {request.SessionId}: paciente sin email.");
                return;
            }

            var host = _configuration["Reminders:Email:SmtpHost"];
            var port = Math.Max(1, _configuration.GetValue<int?>("Reminders:Email:SmtpPort") ?? 587);
            var user = _configuration["Reminders:Email:Username"];
            var pass = _configuration["Reminders:Email:Password"];
            var from = _configuration["Reminders:Email:From"];
            var useSsl = _configuration.GetValue<bool?>("Reminders:Email:EnableSsl") ?? true;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                result.Errors.Add("Canal email habilitado, pero falta configuración SmtpHost o From.");
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl = useSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                if (!string.IsNullOrWhiteSpace(user))
                    smtp.Credentials = new NetworkCredential(user, pass ?? string.Empty);

                using var mail = new MailMessage(from, request.PacienteEmail)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                await smtp.SendMailAsync(mail);
                result.EmailSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando recordatorio email para sesión {SessionId}", request.SessionId);
                result.Errors.Add($"Email sesión {request.SessionId}: {ex.Message}");
            }
        }

        private async Task TrySendWhatsAppAsync(ReminderDeliveryRequest request, string message, ReminderDeliveryResult result, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PacienteTelefono))
            {
                result.Errors.Add($"Sesión {request.SessionId}: paciente sin teléfono.");
                return;
            }

            var apiUrl = _configuration["Reminders:WhatsApp:ApiUrl"];
            var apiToken = _configuration["Reminders:WhatsApp:ApiToken"];
            var timeoutSeconds = Math.Max(3, _configuration.GetValue<int?>("Reminders:WhatsApp:TimeoutSeconds") ?? 15);

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                result.Errors.Add("Canal WhatsApp habilitado, pero falta ApiUrl.");
                return;
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = JsonContent.Create(new
                    {
                        to = NormalizePhone(request.PacienteTelefono),
                        text = message
                    })
                };

                if (!string.IsNullOrWhiteSpace(apiToken))
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var response = await client.SendAsync(requestMessage, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    result.WhatsAppSent = true;
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                result.Errors.Add($"WhatsApp sesión {request.SessionId}: {(int)response.StatusCode} {response.ReasonPhrase} - {TrimForLog(body, 180)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando recordatorio WhatsApp para sesión {SessionId}", request.SessionId);
                result.Errors.Add($"WhatsApp sesión {request.SessionId}: {ex.Message}");
            }
        }

        private string BuildEmailSubject(ReminderDeliveryRequest request)
        {
            var template = _configuration["Reminders:Templates:Email:Subject"]
                ?? "Recordatorio de sesión - {{SessionDateTime}}";

            return ApplyTemplate(template, BuildTemplateContext(request));
        }

        private string BuildEmailBody(ReminderDeliveryRequest request)
        {
            var template = _configuration["Reminders:Templates:Email:Body"];
            if (string.IsNullOrWhiteSpace(template))
            {
                return BuildDefaultBody(request);
            }

            return ApplyTemplate(template, BuildTemplateContext(request));
        }

        private string BuildWhatsAppBody(ReminderDeliveryRequest request)
        {
            var template = _configuration["Reminders:Templates:WhatsApp:Body"];
            if (string.IsNullOrWhiteSpace(template))
            {
                return BuildDefaultBody(request);
            }

            return ApplyTemplate(template, BuildTemplateContext(request));
        }

        private string BuildDefaultBody(ReminderDeliveryRequest request)
        {
            var clinicName = GetClinicName();
            var signature = GetSignature();

            var sb = new StringBuilder();
            sb.AppendLine($"Hola {request.PacienteNombre}, este es un recordatorio de {clinicName}.");
            sb.AppendLine($"Sesión: {request.FechaHora:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Profesional: {request.ProfesionalNombre}");
            if (!string.IsNullOrWhiteSpace(request.TratamientoDescripcion))
                sb.AppendLine($"Tratamiento: {request.TratamientoDescripcion}");
            sb.AppendLine();
            sb.AppendLine($"Confirmar asistencia: {request.ConfirmUrl}");
            sb.AppendLine($"Cancelar sesión: {request.CancelUrl}");

            if (!string.IsNullOrWhiteSpace(signature))
            {
                sb.AppendLine();
                sb.AppendLine(signature);
            }

            return sb.ToString();
        }

        private Dictionary<string, string> BuildTemplateContext(ReminderDeliveryRequest request)
        {
            var sessionDateTime = request.FechaHora.ToString("dd/MM/yyyy HH:mm");
            var clinicName = GetClinicName();
            var signature = GetSignature();
            var contactPhone = _configuration["Reminders:Brand:ContactPhone"] ?? string.Empty;
            var contactEmail = _configuration["Reminders:Brand:ContactEmail"] ?? string.Empty;

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClinicName"] = clinicName,
                ["PatientName"] = request.PacienteNombre,
                ["SessionDateTime"] = sessionDateTime,
                ["ProfessionalName"] = request.ProfesionalNombre,
                ["TreatmentDescription"] = request.TratamientoDescripcion ?? string.Empty,
                ["ConfirmUrl"] = request.ConfirmUrl,
                ["CancelUrl"] = request.CancelUrl,
                ["Signature"] = signature,
                ["ContactPhone"] = contactPhone,
                ["ContactEmail"] = contactEmail
            };
        }

        private static string ApplyTemplate(string template, Dictionary<string, string> context)
        {
            var result = template;
            foreach (var entry in context)
            {
                result = result.Replace("{{" + entry.Key + "}}", entry.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Permite escribir \n en appsettings para saltos de línea reales
            result = result.Replace("\\n", Environment.NewLine, StringComparison.Ordinal);
            return result;
        }

        private string GetClinicName()
            => _configuration["Reminders:Brand:ClinicName"]
                ?? _configuration["Reminders:ClinicName"]
                ?? "KineGestión";

        private string GetSignature()
            => _configuration["Reminders:Brand:Signature"]
                ?? "Equipo " + GetClinicName();

        private string NormalizePhone(string? raw)
        {
            var digits = new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            digits = digits.TrimStart('0');
            var defaultCountryCode = (_configuration["Reminders:WhatsApp:DefaultCountryCode"] ?? "54").Trim();

            if (digits.Length <= 10 && !digits.StartsWith(defaultCountryCode, StringComparison.Ordinal))
                digits = defaultCountryCode + digits;

            return digits;
        }

        private static string TrimForLog(string text, int max)
            => string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max);
    }
}
