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
using Microsoft.Extensions.Configuration;
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
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();

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

            billingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object, billingAlertService: billingAlertService.Object);

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

            sessionService.Verify(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Exactly(3));
            auditLogService.Verify(a => a.GetPagedAsync("ReminderDispatch", null, null, "Create", null, null, 1, 20), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposeBillingNotification_WhenTwoConsecutiveWeeksAreBelowThreshold()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Array.Empty<SessionReminderCandidateDto>());

            auditLogService
                .Setup(a => a.GetPagedAsync("ReminderDispatch", null, null, "Create", null, null, 1, 20))
                .ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));

            billingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot
                {
                    ThresholdPct = 70m,
                    HasConsecutiveLowWeeks = true,
                    TrendPoints = new List<ReminderBillingTrendPointViewModel>
                    {
                        new ReminderBillingTrendPointViewModel { Label = "A", RequestedCount = 10, UpdatedCount = 5, SkippedCount = 5 },
                        new ReminderBillingTrendPointViewModel { Label = "B", RequestedCount = 8, UpdatedCount = 4, SkippedCount = 4 }
                    }
                });

            var controller = BuildController(
                sessionService.Object,
                reminderDispatchQueue.Object,
                reminderDeliveryService.Object,
                auditLogService.Object,
                configurationValues: new Dictionary<string, string?>
                {
                    ["Billing:BatchEffectivenessWarnThresholdPct"] = "70"
                },
                billingAlertService: billingAlertService.Object);

            var result = await controller.Index(24);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReminderCampaignViewModel>(view.Model);
            Assert.True(model.HasBillingBatchConsecutiveLowWeeks);
            Assert.Equal(2, model.BillingBatchWeeklyTrendPoints.Count);
        }

        [Fact]
        public async Task DispatchSelected_ShouldSetError_WhenNothingSelected()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.DispatchSelected(24, Array.Empty<int>());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Seleccioná al menos una sesión para enviar recordatorios.", controller.TempData["Error"]);

            reminderDispatchQueue.Verify(s => s.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default), Times.Never);
            auditLogService.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }

        [Fact]
        public async Task DispatchSelected_ShouldQueueSelectedSessions_ForBackgroundDelivery()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

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

            reminderDispatchQueue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default))
                .Returns(new ValueTask());

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object, "admin@kinegestion.com");

            var result = await controller.DispatchSelected(24, new[] { 7, 999 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);

            Assert.Equal("Recordatorios encolados: 1 de 2. Se procesarán en segundo plano.", controller.TempData["Success"]);
            Assert.Contains("Sesión 999", controller.TempData["Error"]?.ToString());

            reminderDispatchQueue.Verify(q => q.QueueAsync(It.Is<ReminderDispatchWorkItem>(w => w.SessionId == 7 && w.ChangedBy == "admin@kinegestion.com"), default), Times.Once);
            reminderDeliveryService.Verify(d => d.SendAsync(It.IsAny<ReminderDeliveryRequest>(), default), Times.Never);
            auditLogService.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }

        [Fact]
        public async Task DispatchOperational_ShouldQueueDistinctSessions_FromConfiguredWindows()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.Is<DateTime>(to => to <= DateTime.UtcNow.AddHours(4))))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(101, DateTime.UtcNow.AddHours(2), "A", "a@test.com", "549111", "Prof A", "Tx")
                });

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.Is<DateTime>(to => to > DateTime.UtcNow.AddHours(4))))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(101, DateTime.UtcNow.AddHours(2), "A", "a@test.com", "549111", "Prof A", "Tx"),
                    new SessionReminderCandidateDto(202, DateTime.UtcNow.AddHours(10), "B", "b@test.com", "549222", "Prof B", "Tx")
                });

            reminderDispatchQueue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default))
                .Returns(new ValueTask());

            var controller = BuildController(
                sessionService.Object,
                reminderDispatchQueue.Object,
                reminderDeliveryService.Object,
                auditLogService.Object,
                "admin@kinegestion.com",
                new Dictionary<string, string?> { ["Reminders:OperationalWindowsHours"] = "12,3" });

            var result = await controller.DispatchOperational(24);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            reminderDispatchQueue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default), Times.Exactly(2));
        }

        [Fact]
        public async Task DispatchOperational_ShouldQueueBillingAlert_WhenLowTrendIsDetectedAndNotSentToday()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new[]
                {
                    new SessionReminderCandidateDto(101, DateTime.UtcNow.AddHours(2), "A", "a@test.com", "549111", "Prof A", "Tx")
                });

            billingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            billingAlertService
                .Setup(s => s.QueueAlertIfNeededAsync(It.IsAny<string?>(), It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertDispatchResult { Queued = true, Message = "Alerta operativa de cobranzas encolada para administración." });

            reminderDispatchQueue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default))
                .Returns(new ValueTask());

            var controller = BuildController(
                sessionService.Object,
                reminderDispatchQueue.Object,
                reminderDeliveryService.Object,
                auditLogService.Object,
                "admin@kinegestion.com",
                new Dictionary<string, string?>
                {
                    ["Reminders:OperationalWindowsHours"] = "12,3",
                    ["Billing:BatchEffectivenessWarnThresholdPct"] = "70",
                    ["Reminders:OperationalAlerts:AdminEmail"] = "ops@kinegestion.com"
                },
                billingAlertService.Object);

            var result = await controller.DispatchOperational(24);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            billingAlertService.Verify(s => s.QueueAlertIfNeededAsync("admin@kinegestion.com", It.IsAny<DateTime>(), default), Times.Once);
            Assert.Contains("Alerta operativa de cobranzas encolada", controller.TempData["Success"]?.ToString());
        }

        [Fact]
        public async Task Respond_ShouldConfirm_WhenTokenIsValid()
        {
            var sessionService = new Mock<ISessionService>();
            var reminderDeliveryService = new Mock<IReminderDeliveryService>();
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            sessionService.Setup(s => s.ConfirmByReminderAsync(5)).Returns(Task.CompletedTask);

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object);
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
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object);

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
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

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

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object);

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
            var reminderDispatchQueue = new Mock<IReminderDispatchQueue>();
            var auditLogService = new Mock<IAuditLogService>();

            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            sessionService
                .Setup(s => s.GetReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Array.Empty<SessionReminderCandidateDto>());

            var controller = BuildController(sessionService.Object, reminderDispatchQueue.Object, reminderDeliveryService.Object, auditLogService.Object);

            var result = await controller.SendTest(24, null, null, null, true);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("No hay sesiones disponibles en la ventana para ejecutar una prueba.", controller.TempData["Error"]);
            reminderDeliveryService.Verify(d => d.BuildPreview(It.IsAny<ReminderDeliveryRequest>()), Times.Never);
        }

        private static RemindersController BuildController(
            ISessionService sessionService,
            IReminderDispatchQueue reminderDispatchQueue,
            IReminderDeliveryService reminderDeliveryService,
            IAuditLogService auditLogService,
            string? userName = null,
            IDictionary<string, string?>? configurationValues = null,
            IBillingOperationalAlertService? billingAlertService = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>())
                .Build();

            var resolvedBillingAlertService = billingAlertService ?? BuildBillingAlertServiceMock().Object;

            var controller = new RemindersController(
                sessionService,
                new FakeDataProtectionProvider(),
                reminderDispatchQueue,
                reminderDeliveryService,
                auditLogService,
                resolvedBillingAlertService,
                configuration);

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

        private static Mock<IBillingOperationalAlertService> BuildBillingAlertServiceMock()
        {
            var service = new Mock<IBillingOperationalAlertService>();
            service
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });
            service
                .Setup(s => s.QueueAlertIfNeededAsync(It.IsAny<string?>(), It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertDispatchResult { Message = string.Empty });
            return service;
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
