using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace KineGestion.Web.Tests
{
    public class RemindersControllerTests
    {
        [Fact]
        public async Task Index_ShouldClampHoursAndPopulateItemsAndHistory()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(
                        10,
                        new DateTime(2026, 5, 29, 18, 0, 0),
                        "Perez, Juan",
                        "juan@test.com",
                        "5491122334455",
                        "Gomez, Ana",
                        "Rehabilitacion")
                });

            auditLogService
                .Setup(a => a.GetPagedAsync("ReminderDispatch", null, null, "Create", null, null, 1, 20))
                .ReturnsAsync((
                    new[]
                    {
                        new AuditLog
                        {
                            EntityId = "10",
                            ChangedAt = new DateTime(2026, 5, 29, 16, 0, 0),
                            ChangedBy = "admin@kinegestion.com",
                            NewValuesJson = "{\"EmailSent\":true,\"WhatsAppSent\":false,\"Errors\":[]}"
                        }
                    }.AsEnumerable(),
                    1));

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.Index(999);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReminderCampaignViewModel>(view.Model);

            Assert.Equal(168, model.HoursAhead);
            Assert.Single(model.Items);
            Assert.Equal(10, model.Items[0].SessionId);
            Assert.NotEmpty(model.Items[0].ConfirmUrl);
            Assert.NotEmpty(model.Items[0].CancelUrl);
            Assert.Single(model.History);
            Assert.Equal("Enviado", model.History[0].Status);
            Assert.Equal("Email", model.History[0].ChannelSummary);

            sessionService.Verify(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            auditLogService.Verify(a => a.GetPagedAsync("ReminderDispatch", null, null, "Create", null, null, 1, 20), Times.Once);
        }

        [Fact]
        public async Task DispatchSelected_ShouldSetError_WhenNothingSelected()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.DispatchSelected(24, Array.Empty<int>());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Seleccioná al menos una sesión para enviar recordatorios.", controller.TempData["Error"]);

            reminderDeliveryService.Verify(s => s.SendAsync(It.IsAny<ReminderDeliveryRequest>(), default), Times.Never);
            auditLogService.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }

        [Fact]
        public async Task DispatchSelected_ShouldSendAndRegisterAudit_ForSelectedSessions()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(
                        7,
                        new DateTime(2026, 5, 29, 19, 30, 0),
                        "Lopez, Maria",
                        "maria@test.com",
                        "5491188877766",
                        "Suarez, Pablo",
                        "Post operatorio")
                });

            reminderDeliveryService
                .Setup(d => d.SendAsync(It.IsAny<ReminderDeliveryRequest>(), default))
                .ReturnsAsync(new ReminderDeliveryResult { EmailSent = true });

            auditLogService
                .Setup(a => a.AddAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync((AuditLog l) => l);

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object, "admin@kinegestion.com");

            var result = await controller.DispatchSelected(24, new[] { 7, 999 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);

            Assert.Equal("Recordatorios enviados: 1 de 2.", controller.TempData["Success"]);
            Assert.Contains("Sesión 999", controller.TempData["Error"]?.ToString());

            reminderDeliveryService.Verify(d => d.SendAsync(It.Is<ReminderDeliveryRequest>(r => r.SessionId == 7), default), Times.Once);
            auditLogService.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.EntityName == "ReminderDispatch" && l.EntityId == "7" && l.Action == "Create")), Times.Once);
        }

        [Fact]
        public async Task Respond_ShouldConfirm_WhenTokenIsValid()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            sessionService.Setup(s => s.ConfirmByReminderAsync(5)).Returns(Task.CompletedTask);

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);
            var token = BuildProtectedToken(5, "confirm", DateTime.UtcNow.AddHours(2));

            var result = await controller.Respond(5, "confirm", token);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Result", view.ViewName);
            var model = Assert.IsType<ReminderResponseViewModel>(view.Model);
            Assert.True(model.Success);
            Assert.Equal("Asistencia confirmada", model.Title);
            sessionService.Verify(s => s.ConfirmByReminderAsync(5), Times.Once);
        }

        [Fact]
        public async Task Respond_ShouldReturnInvalidLink_WhenTokenIsInvalid()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.Respond(5, "confirm", "token-invalido");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Result", view.ViewName);
            var model = Assert.IsType<ReminderResponseViewModel>(view.Model);
            Assert.False(model.Success);
            Assert.Equal("Enlace inválido", model.Title);
            sessionService.Verify(s => s.ConfirmByReminderAsync(It.IsAny<int>()), Times.Never);
            sessionService.Verify(s => s.CancelByReminderAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SendTest_ShouldReturnPreviewView_WhenDryRunIsTrue()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(
                        14,
                        new DateTime(2026, 5, 29, 20, 0, 0),
                        "Mendez, Laura",
                        "laura@test.com",
                        "5491177776666",
                        "Rios, Carla",
                        "Dolor lumbar")
                });

            reminderDeliveryService
                .Setup(d => d.BuildPreview(It.IsAny<ReminderDeliveryRequest>()))
                .Returns(new ReminderPreviewResult
                {
                    EmailSubject = "Asunto preview",
                    EmailBody = "Cuerpo email preview",
                    WhatsAppBody = "Cuerpo whatsapp preview",
                    CanEmail = true,
                    CanWhatsApp = true
                });

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.SendTest(24, 14, "qa@kine.com", "5491199998888", true);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("TestResult", view.ViewName);
            var model = Assert.IsType<ReminderTestResultViewModel>(view.Model);
            Assert.True(model.DryRun);
            Assert.Equal(14, model.SessionId);
            Assert.Equal("qa@kine.com", model.DestinoEmail);
            Assert.Equal("5491199998888", model.DestinoWhatsApp);
            Assert.Equal("Asunto preview", model.EmailSubject);

            reminderDeliveryService.Verify(d => d.BuildPreview(It.IsAny<ReminderDeliveryRequest>()), Times.Once);
            reminderDeliveryService.Verify(d => d.SendAsync(It.IsAny<ReminderDeliveryRequest>(), default), Times.Never);
        }

        [Fact]
        public async Task SendTest_ShouldRedirectWithError_WhenNoCandidateExists()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var auditLogService = new Mock<IAuditLogService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Array.Empty<SessionReminderCandidateDto>());

            var controller = BuildController(sessionService.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.SendTest(24, null, null, null, true);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("No hay sesiones disponibles en la ventana para ejecutar una prueba.", controller.TempData["Error"]);
            reminderDeliveryService.Verify(d => d.BuildPreview(It.IsAny<ReminderDeliveryRequest>()), Times.Never);
        }

        private static RemindersController BuildController(
            ISessionService sessionService,
            IReminderDeliveryService reminderDeliveryService,
            IAuditLogService auditLogService,
            string? userName = null)
        {
            var controller = new RemindersController(
                sessionService,
                new FakeDataProtectionProvider(),
                reminderDeliveryService,
                auditLogService);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            if (!string.IsNullOrWhiteSpace(userName))
            {
                httpContext.User = new ClaimsPrincipal(
                    new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName) }, "TestAuth"));
            }

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            var url = new Mock<IUrlHelper>();
            url
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://localhost:5138/Reminders/Respond");
            controller.Url = url.Object;

            return controller;
        }

        private static string BuildProtectedToken(int sessionId, string action, DateTime expiresUtc)
        {
            var payload = string.Join("|", sessionId, action, expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            var protectedPayload = "p:" + payload;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(protectedPayload));
        }

        private sealed class FakeDataProtectionProvider : IDataProtectionProvider
        {
            public IDataProtector CreateProtector(string purpose)
                => new FakeDataProtector();
        }

        private sealed class FakeDataProtector : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose)
                => this;

            public byte[] Protect(byte[] plaintext)
            {
                var text = Encoding.UTF8.GetString(plaintext);
                return Encoding.UTF8.GetBytes("p:" + text);
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                var text = Encoding.UTF8.GetString(protectedData);
                if (!text.StartsWith("p:", StringComparison.Ordinal))
                    throw new CryptographicException("token inválido");

                return Encoding.UTF8.GetBytes(text.Substring(2));
            }
        }
    }
}
