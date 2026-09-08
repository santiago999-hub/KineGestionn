using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KineGestion.Web.Tests
{
    public class ReminderDeliveryServiceTests
    {
        [Fact]
        public async Task SendAsync_ShouldReturnError_WhenNoChannelEnabled()
        {
            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.AnyChannelSent);
            Assert.Contains("Ningún canal de envío está habilitado en configuración.", result.Errors);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnError_WhenWhatsAppEnabledWithoutApiUrl()
        {
            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.WhatsAppSent);
            Assert.Contains("Canal WhatsApp habilitado, pero falta ApiUrl.", result.Errors);
        }

        [Fact]
        public async Task SendAsync_ShouldUseWhatsAppTemplate_WithPlaceholderReplacement()
        {
            string? capturedText = null;
            var handler = new CaptureHandler(async (req, ct) =>
            {
                var payloadJson = await req.Content!.ReadAsStringAsync(ct);
                using var payload = JsonDocument.Parse(payloadJson);
                capturedText = payload.RootElement.GetProperty("text").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true",
                    ["Reminders:WhatsApp:ApiUrl"] = "https://api.whatsapp.test/send",
                    ["Reminders:WhatsApp:DefaultCountryCode"] = "54",
                    ["Reminders:Templates:WhatsApp:Body"] = "Hola {{PatientName}}\\nConfirma: {{ConfirmUrl}}\\nFirma: {{Signature}}",
                    ["Reminders:Brand:Signature"] = "Equipo Kine X"
                },
                handler);

            var result = await service.SendAsync(BuildRequest());

            Assert.True(result.WhatsAppSent);
            Assert.True(result.AnyChannelSent);
            Assert.NotNull(capturedText);
            Assert.Contains("Hola Perez, Juan", capturedText);
            Assert.Contains("Confirma: https://confirm.test", capturedText);
            Assert.Contains("Firma: Equipo Kine X", capturedText);
            Assert.Contains(Environment.NewLine, capturedText);
        }

        [Fact]
        public async Task SendAsync_ShouldUseDefaultBody_WhenTemplateMissing()
        {
            string? capturedText = null;
            var handler = new CaptureHandler(async (req, ct) =>
            {
                var payloadJson = await req.Content!.ReadAsStringAsync(ct);
                using var payload = JsonDocument.Parse(payloadJson);
                capturedText = payload.RootElement.GetProperty("text").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true",
                    ["Reminders:WhatsApp:ApiUrl"] = "https://api.whatsapp.test/send",
                    ["Reminders:Brand:ClinicName"] = "KineBrand",
                    ["Reminders:Brand:Signature"] = "Firma Brand"
                },
                handler);

            var result = await service.SendAsync(BuildRequest());

            Assert.True(result.WhatsAppSent);
            Assert.NotNull(capturedText);
            Assert.Contains("recordatorio de KineBrand", capturedText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Firma Brand", capturedText);
            Assert.Contains("Cancelar sesión: https://cancel.test", capturedText);
        }

        [Fact]
        public async Task SendAsync_ShouldReportMissingPhone_WhenWhatsAppEnabled()
        {
            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true",
                    ["Reminders:WhatsApp:ApiUrl"] = "https://api.whatsapp.test/send"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var req = BuildRequest();
            req.PacienteTelefono = null;

            var result = await service.SendAsync(req);

            Assert.False(result.WhatsAppSent);
            Assert.Contains("paciente sin teléfono", string.Join(" | ", result.Errors), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendAsync_ShouldUseOverrideContent_WhenOperationalAlertIsQueued()
        {
            string? capturedText = null;
            var handler = new CaptureHandler(async (req, ct) =>
            {
                var payloadJson = await req.Content!.ReadAsStringAsync(ct);
                using var payload = JsonDocument.Parse(payloadJson);
                capturedText = payload.RootElement.GetProperty("text").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = BuildService(
                new Mock<IEmailSender>(),
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true",
                    ["Reminders:WhatsApp:ApiUrl"] = "https://api.whatsapp.test/send"
                },
                handler);

            var request = BuildRequest();
            request.WhatsAppBodyOverride = "ALERTA OPERATIVA";

            var result = await service.SendAsync(request);

            Assert.True(result.WhatsAppSent);
            Assert.Equal("ALERTA OPERATIVA", capturedText);
        }

        [Fact]
        public async Task SendAsync_WhenEmailEnabled_ShouldForwardEnvelopeToSender()
        {
            EmailEnvelope? captured = null;
            var emailSender = new Mock<IEmailSender>();
            emailSender.SetupGet(s => s.IsConfigured).Returns(true);
            emailSender
                .Setup(s => s.SendAsync(It.IsAny<EmailEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<EmailEnvelope, CancellationToken>((envelope, _) => captured = envelope)
                .Returns(Task.CompletedTask);

            var service = BuildService(
                emailSender,
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "true",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var result = await service.SendAsync(BuildRequest());

            Assert.True(result.EmailSent);
            Assert.True(result.AnyChannelSent);
            Assert.NotNull(captured);
            Assert.Equal("juan@test.com", captured.To);
            Assert.Contains("Perez, Juan", captured.Body);
            Assert.Contains("https://confirm.test", captured.Body);
            emailSender.Verify(s => s.SendAsync(It.IsAny<EmailEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenEmailEnabledAndSenderFails_ShouldRecordError()
        {
            var emailSender = new Mock<IEmailSender>();
            emailSender.SetupGet(s => s.IsConfigured).Returns(true);
            emailSender
                .Setup(s => s.SendAsync(It.IsAny<EmailEnvelope>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("smtp down"));

            var service = BuildService(
                emailSender,
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "true",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.EmailSent);
            Assert.False(result.AnyChannelSent);
            Assert.Contains("smtp down", string.Join(" | ", result.Errors), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendAsync_WhenEmailEnabledButNotConfigured_ShouldRecordConfigurationError()
        {
            var emailSender = new Mock<IEmailSender>();
            emailSender.SetupGet(s => s.IsConfigured).Returns(false);

            var service = BuildService(
                emailSender,
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "true",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.EmailSent);
            Assert.Contains("falta configuración SmtpHost o From", string.Join(" | ", result.Errors), StringComparison.OrdinalIgnoreCase);
            emailSender.Verify(s => s.SendAsync(It.IsAny<EmailEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_WhenEmailEnabledAndPatientWithoutEmail_ShouldReportMissingEmail()
        {
            var emailSender = new Mock<IEmailSender>();
            emailSender.SetupGet(s => s.IsConfigured).Returns(true);

            var service = BuildService(
                emailSender,
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "true",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var request = BuildRequest();
            request.PacienteEmail = null;

            var result = await service.SendAsync(request);

            Assert.False(result.EmailSent);
            Assert.Contains("paciente sin email", string.Join(" | ", result.Errors), StringComparison.OrdinalIgnoreCase);
            emailSender.Verify(s => s.SendAsync(It.IsAny<EmailEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static ReminderDeliveryService BuildService(
            Mock<IEmailSender> emailSender,
            Dictionary<string, string?> values,
            HttpMessageHandler handler)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var logger = new Mock<ILogger<ReminderDeliveryService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));

            return new ReminderDeliveryService(config, httpFactory.Object, emailSender.Object, logger.Object);
        }

        private static ReminderDeliveryRequest BuildRequest() => new()
        {
            SessionId = 12,
            FechaHora = new DateTime(2026, 5, 30, 10, 30, 0),
            PacienteNombre = "Perez, Juan",
            PacienteEmail = "juan@test.com",
            PacienteTelefono = "+54 9 11 2233-4455",
            ProfesionalNombre = "Gomez, Ana",
            TratamientoDescripcion = "Rehabilitacion",
            ConfirmUrl = "https://confirm.test",
            CancelUrl = "https://cancel.test"
        };

        private sealed class CaptureHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public CaptureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _handler(request, cancellationToken);
        }
    }
}