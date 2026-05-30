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
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "false"
                },
                new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.AnyChannelSent);
            Assert.Contains("Ningún canal de envío está habilitado en configuración.", result.Errors);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnError_WhenWhatsAppEnabledWithoutApiUrl()
        {
            var service = BuildService(
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true"
                },
                new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var result = await service.SendAsync(BuildRequest());

            Assert.False(result.WhatsAppSent);
            Assert.Contains("Canal WhatsApp habilitado, pero falta ApiUrl.", result.Errors);
        }

        [Fact]
        public async Task SendAsync_ShouldUseWhatsAppTemplate_WithPlaceholderReplacement()
        {
            string? capturedText = null;
            var handler = new CaptureHandler(req =>
            {
                var payloadJson = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var payload = JsonDocument.Parse(payloadJson);
                capturedText = payload.RootElement.GetProperty("text").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = BuildService(
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
            var handler = new CaptureHandler(req =>
            {
                var payloadJson = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var payload = JsonDocument.Parse(payloadJson);
                capturedText = payload.RootElement.GetProperty("text").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = BuildService(
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
                new Dictionary<string, string?>
                {
                    ["Reminders:Email:Enabled"] = "false",
                    ["Reminders:WhatsApp:Enabled"] = "true",
                    ["Reminders:WhatsApp:ApiUrl"] = "https://api.whatsapp.test/send"
                },
                new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var req = BuildRequest();
            req.PacienteTelefono = null;

            var result = await service.SendAsync(req);

            Assert.False(result.WhatsAppSent);
            Assert.Contains("paciente sin teléfono", string.Join(" | ", result.Errors), StringComparison.OrdinalIgnoreCase);
        }

        private static ReminderDeliveryService BuildService(Dictionary<string, string?> values, HttpMessageHandler handler)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var logger = new Mock<ILogger<ReminderDeliveryService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));

            return new ReminderDeliveryService(config, httpFactory.Object, logger.Object);
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
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = _handler(request);
                return Task.FromResult(response);
            }
        }
    }
}
