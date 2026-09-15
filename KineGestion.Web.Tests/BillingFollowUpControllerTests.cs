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

            var dispatchRepository = new Mock<IDispatchEventRepository>();
            dispatchRepository
                .Setup(r => r.GetByTypePrefixAsync("BillingFollowUp:", null, null))
                .ReturnsAsync(Array.Empty<DispatchEvent>());

            var queued = new List<ReminderDispatchWorkItem>();

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(queued).Object,
                new Mock<IReminderDeliveryService>().Object,
                dispatchRepository.Object,
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
                new Mock<IDispatchEventRepository>().Object,
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
                new Mock<IDispatchEventRepository>().Object,
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
                new Mock<IDispatchEventRepository>().Object,
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
                new Mock<IDispatchEventRepository>().Object,
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
        public async Task Index_ShouldMapDispatchEventHistoryFromRepository()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<BillingFollowUpCandidateDto>());

            var dispatchRepository = new Mock<IDispatchEventRepository>();
            dispatchRepository
                .Setup(r => r.GetByTypePrefixAsync("BillingFollowUp:", null, null))
                .ReturnsAsync(new[]
                {
                    new DispatchEvent
                    {
                        DispatchType = "BillingFollowUp:Soft",
                        SessionId = 42,
                        SentAtUtc = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc),
                        ChangedBy = "admin@local",
                        EmailSent = true,
                        WhatsAppSent = true
                    },
                    new DispatchEvent
                    {
                        DispatchType = "BillingFollowUp:Firm",
                        SessionId = 43,
                        SentAtUtc = new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc),
                        ChangedBy = "admin@local",
                        EmailSent = false,
                        WhatsAppSent = false,
                        Errors = "boom"
                    }
                });

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(new List<ReminderDispatchWorkItem>()).Object,
                new Mock<IReminderDeliveryService>().Object,
                dispatchRepository.Object,
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
            dispatchRepository.Verify(r => r.GetByTypePrefixAsync("BillingFollowUp:", null, null), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldHandleDispatchEventsWithoutSessionId_AndWithoutErrors()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<BillingFollowUpCandidateDto>());

            var dispatchRepository = new Mock<IDispatchEventRepository>();
            dispatchRepository
                .Setup(r => r.GetByTypePrefixAsync("BillingFollowUp:", null, null))
                .ReturnsAsync(new[]
                {
                    new DispatchEvent
                    {
                        DispatchType = "BillingFollowUp:Soft",
                        SessionId = null,
                        SentAtUtc = DateTime.UtcNow,
                        ChangedBy = "admin@local",
                        EmailSent = false,
                        WhatsAppSent = false,
                        Errors = null
                    }
                });

            var controller = new BillingFollowUpController(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                BuildQueueMock(new List<ReminderDispatchWorkItem>()).Object,
                new Mock<IReminderDeliveryService>().Object,
                dispatchRepository.Object,
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
            Assert.Equal("Sin envío", item.ChannelSummary);
            Assert.Null(item.ErrorSummary);
            Assert.Equal(0, item.SessionId);
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
