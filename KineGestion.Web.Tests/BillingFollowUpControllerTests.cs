using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KineGestion.Web.Tests
{
    public class BillingFollowUpControllerTests
    {
        [Fact]
        public async Task Index_ShouldGroupCandidatesIntoConfiguredTiers()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[]
                {
                    Candidate(1, days: 1),  // Soft
                    Candidate(2, days: 5),  // Reminder
                    Candidate(3, days: 9)   // Firm
                });

            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetPagedAsync("BillingFollowUp", null, null, "Create", null, null, 1, 20))
                .ReturnsAsync((Enumerable.Empty<AuditLog>(), 0));

            var queued = new List<ReminderDispatchWorkItem>();

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(queued).Object,
                new Mock<IReminderDeliveryService>().Object,
                auditLogService.Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.TempData = MakeTempData(controller);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingFollowUpCampaignViewModel>(view.Model);

            Assert.Equal(3, model.TotalCandidates);
            Assert.Equal(3, model.TierGroups.Count);
            Assert.Equal("D+1 - Suave", model.TierGroups[0].Label);
            Assert.Contains(model.TierGroups[0].Items, i => i.SessionId == 1);
            Assert.Contains(model.TierGroups[1].Items, i => i.SessionId == 2);
            Assert.Contains(model.TierGroups[2].Items, i => i.SessionId == 3);
            Assert.Empty(model.History);
        }

        [Fact]
        public async Task DispatchSelected_ShouldQueueOnlySelectedAndEligibleCandidates()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[]
                {
                    Candidate(10, days: 1),
                    Candidate(11, days: 5)
                });

            var queued = new List<ReminderDispatchWorkItem>();
            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(queued).Object,
                new Mock<IReminderDeliveryService>().Object,
                new Mock<IAuditLogService>().Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.TempData = MakeTempData(controller);

            var result = await controller.DispatchSelected(new[] { 10, 99 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BillingFollowUpController.Index), redirect.ActionName);

            var queuedSession = Assert.Single(queued);
            Assert.Equal(10, queuedSession.SessionId);
            Assert.Equal("BillingFollowUp:Soft", queuedSession.DispatchType);

            Assert.NotNull(controller.TempData["Success"]);
            Assert.Contains("Envíos encolados: 1 de 2", (string)controller.TempData["Success"]!);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task DispatchSelected_WithNoSelection_ShouldNotQueueAnything()
        {
            var queued = new List<ReminderDispatchWorkItem>();
            var controller = new BillingFollowUpController(
                new Mock<ISessionService>().Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(queued).Object,
                new Mock<IReminderDeliveryService>().Object,
                new Mock<IAuditLogService>().Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.TempData = MakeTempData(controller);

            var result = await controller.DispatchSelected(Array.Empty<int>());

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(queued);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task DispatchTier_ShouldQueueAllCandidatesMatchingThatTier()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[]
                {
                    Candidate(20, days: 4),
                    Candidate(21, days: 6),
                    Candidate(22, days: 9)
                });

            var queued = new List<ReminderDispatchWorkItem>();
            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(queued).Object,
                new Mock<IReminderDeliveryService>().Object,
                new Mock<IAuditLogService>().Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.TempData = MakeTempData(controller);

            var result = await controller.DispatchTier(3, 6);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(2, queued.Count);
            Assert.All(queued, q => Assert.Equal("BillingFollowUp:Reminder", q.DispatchType));
        }

        [Fact]
        public async Task SendTest_DryRun_ShouldPreviewWithoutSending()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[] { Candidate(30, days: 2) });

            var delivery = new Mock<IReminderDeliveryService>();
            delivery
                .Setup(d => d.BuildPreview(It.IsAny<ReminderDeliveryRequest>()))
                .Returns(new ReminderPreviewResult
                {
                    EmailSubject = "subject",
                    EmailBody = "body",
                    WhatsAppBody = "wa",
                    CanEmail = true,
                    CanWhatsApp = true,
                    Warnings = new List<string>()
                });

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(new List<ReminderDispatchWorkItem>()).Object,
                delivery.Object,
                new Mock<IAuditLogService>().Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.SendTest(30, null, null, dryRun: true);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingFollowUpTestResultViewModel>(view.Model);
            Assert.True(model.DryRun);
            Assert.Equal(30, model.SessionId);
            Assert.False(model.EmailSent);

            delivery.Verify(d => d.SendAsync(It.IsAny<ReminderDeliveryRequest>(), default), Times.Never);
        }

        [Fact]
        public async Task Index_ShouldParseEmailAndWhatsAppFromAuditHistoryJson()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<BillingFollowUpCandidateDto>());

            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetPagedAsync("BillingFollowUp", null, null, "Create", null, null, 1, 20))
                .ReturnsAsync((new[]
                {
                    new AuditLog
                    {
                        EntityId = "42",
                        ChangedAt = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc),
                        ChangedBy = "admin@local",
                        NewValuesJson = "{\"EmailSent\":true,\"WhatsAppSent\":true}"
                    },
                    new AuditLog
                    {
                        EntityId = "43",
                        ChangedAt = new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc),
                        ChangedBy = "admin@local",
                        NewValuesJson = "{\"EmailSent\":false,\"WhatsAppSent\":false,\"Errors\":[\"boom\"]}"
                    }
                }.AsEnumerable(), 2));

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(new List<ReminderDispatchWorkItem>()).Object,
                new Mock<IReminderDeliveryService>().Object,
                auditLogService.Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingFollowUpCampaignViewModel>(view.Model);

            Assert.Equal(2, model.History.Count);
            Assert.Equal("Email + WhatsApp", model.History[0].ChannelSummary);
            Assert.Equal("Enviado", model.History[0].Status);
            Assert.Equal("Sin envío", model.History[1].ChannelSummary);
            Assert.Equal("Error", model.History[1].Status);
            Assert.Contains("boom", model.History[1].ErrorSummary);
            Assert.Equal(42, model.History[0].SessionId);
        }

        [Fact]
        public async Task Index_ShouldHandleMalformedHistoryJsonGracefully()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<BillingFollowUpCandidateDto>());

            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetPagedAsync("BillingFollowUp", null, null, "Create", null, null, 1, 20))
                .ReturnsAsync((new[]
                {
                    new AuditLog
                    {
                        EntityId = "abc",
                        ChangedAt = DateTime.UtcNow,
                        ChangedBy = "admin@local",
                        NewValuesJson = "not-json"
                    }
                }.AsEnumerable(), 1));

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(new List<ReminderDispatchWorkItem>()).Object,
                new Mock<IReminderDeliveryService>().Object,
                auditLogService.Object,
                BuildEmptyConfiguration(),
                new Mock<ILogger<BillingFollowUpController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingFollowUpCampaignViewModel>(view.Model);

            var item = Assert.Single(model.History);
            Assert.Equal("Error", item.Status);
            Assert.Contains("No se pudo interpretar", item.ErrorSummary);
        }

        private static Mock<IReminderDispatchQueue> BuildQueueMock(List<ReminderDispatchWorkItem> queued)
        {
            var queue = new Mock<IReminderDispatchQueue>();
            queue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()))
                .Callback<ReminderDispatchWorkItem, CancellationToken>((item, _) => queued.Add(item))
                .Returns(new ValueTask());
            return queue;
        }

        private static TempDataDictionary MakeTempData(Controller controller)
            => new(controller.HttpContext, Mock.Of<ITempDataProvider>());

        private static IConfiguration BuildEmptyConfiguration()
            => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        private static BillingFollowUpCandidateDto Candidate(int sessionId, int days)
            => new(
                sessionId,
                new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                "Perez, Juan",
                "juan@test.com",
                "5491122334455",
                "Gomez, Ana",
                "Rehabilitacion",
                days);
    }
}
