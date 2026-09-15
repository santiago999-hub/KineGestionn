using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KineGestion.Web.Tests
{
    public class BillingControllerTests
    {
        [Fact]
        public async Task Index_ShouldPopulateDashboard_UsingDirectCounters()
        {
            var sessionService = new Mock<ISessionService>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:DefaultSessionAmount"] = "2500"
                })
                .Build();

            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(6);
            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(10);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(4);
            sessionService.Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((new[]
                {
                    new SessionListDto(1, DateTime.UtcNow, SessionStatus.Completed, PaymentStatus.Pending, 1, "Paciente", "Pro", "Tx", "Consultorio", false)
                }.AsEnumerable(), 1));

            var controller = BuildController(sessionService.Object, configuration);

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);
            Assert.Equal(6, model.PendingCount);
            Assert.Equal(10, model.PaidCount);
            Assert.Equal(4, model.CompletedPendingCount);
            Assert.Equal(2500m, model.DefaultSessionAmount);
            Assert.Single(model.Items);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldReturnError_WhenNoIdsAreProvided()
        {
            var sessionService = new Mock<ISessionService>();
            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPaidBatch(new List<int>(), null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Seleccioná al menos una sesión pendiente para marcar como pagada.", controller.TempData["Error"]);
            sessionService.Verify(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldMarkDistinctPositiveIds_AsPaid()
        {
            var sessionService = new Mock<ISessionService>();
            var batchRepository = BuildBatchRepositoryMock();
            sessionService
                .Setup(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((2, 0));

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build(), batchRepository);

            var result = await controller.MarkPaidBatch(new List<int> { 7, 7, 0, -2, 9 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("2 sesiones marcadas como pagadas.", controller.TempData["Success"]);
            sessionService.Verify(
                s => s.MarkCompletedPendingAsPaidBatchAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(7) && ids.Contains(9))),
                Times.Once);
            batchRepository.Verify(
                s => s.AddAsync(It.Is<BillingBatchEvent>(e => e.Operation == "MarkPaidBatch" && e.RequestedCount == 2 && e.UpdatedCount == 2 && e.SkippedCount == 0)),
                Times.Once);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldShowMixedResult_WhenSomeSessionsAreSkipped()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((1, 2));

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPaidBatch(new List<int> { 3, 5, 7 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("1 sesiones marcadas como pagadas y 2 omitidas por no estar completadas/pedientes.", controller.TempData["Success"]);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldTruncateSearch_WhenLoggingBatchEvent()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((1, 0));

            BillingBatchEvent? captured = null;
            var batchRepository = BuildBatchRepositoryMock();
            batchRepository
                .Setup(s => s.AddAsync(It.IsAny<BillingBatchEvent>()))
                .Callback<BillingBatchEvent>(e => captured = e)
                .Returns(Task.CompletedTask);

            var longSearch = new string('a', 300);
            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build(), batchRepository);

            var result = await controller.MarkPaidBatch(new List<int> { 3 }, null, null, longSearch);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.NotNull(captured);
            Assert.Equal(200, captured!.FilterSearch!.Length);
            Assert.StartsWith(new string('a', 200), captured.FilterSearch);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldReturnError_WhenNoEligibleSessionsWereUpdated()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((0, 2));

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPaidBatch(new List<int> { 4, 8 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("No se encontraron sesiones completadas y pendientes para actualizar en la selección actual.", controller.TempData["Error"]);
        }

        [Fact]
        public async Task MarkPendingBatch_ShouldReopenDistinctPositiveIds()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.MarkPaidAsPendingBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((2, 1));

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPendingBatch(new List<int> { 2, 2, -3, 4, 6 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("2 sesiones reabiertas y 1 omitidas por no estar pagadas.", controller.TempData["Success"]);
            sessionService.Verify(
                s => s.MarkPaidAsPendingBatchAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 3 && ids.Contains(2) && ids.Contains(4) && ids.Contains(6))),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposeLastBatchKpi_WhenTempDataIsPresent()
        {
            var sessionService = new Mock<ISessionService>();
            var batchRepository = BuildBatchRepositoryMock();
            var configuration = new ConfigurationBuilder().Build();

            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 0));

            var controller = BuildController(sessionService.Object, configuration, batchRepository);
            controller.TempData["BillingBatchRequestedCount"] = 10;
            controller.TempData["BillingBatchUpdatedCount"] = 7;
            controller.TempData["BillingBatchSkippedCount"] = 3;

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);
            Assert.Equal(10, model.LastBatchRequestedCount);
            Assert.Equal(7, model.LastBatchUpdatedCount);
            Assert.Equal(3, model.LastBatchSkippedCount);
            Assert.Equal(70m, model.LastBatchEffectivenessPct);
        }

        [Fact]
        public async Task Index_ShouldAggregateWeeklyBatchMetrics_FromBillingBatchEvents()
        {
            var sessionService = new Mock<ISessionService>();
            var batchRepository = BuildBatchRepositoryMock();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:BatchEffectivenessWarnThresholdPct"] = "80"
                })
                .Build();

            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 0));

            var batchEvents = new[]
            {
                new BillingBatchEvent
                {
                    Operation = "MarkPaidBatch",
                    RequestedCount = 10,
                    UpdatedCount = 7,
                    SkippedCount = 3,
                    CreatedAtUtc = DateTime.UtcNow,
                    ChangedBy = "admin@local"
                },
                new BillingBatchEvent
                {
                    Operation = "MarkPaidBatch",
                    RequestedCount = 5,
                    UpdatedCount = 2,
                    SkippedCount = 3,
                    CreatedAtUtc = DateTime.UtcNow,
                    ChangedBy = "admin@local"
                }
            };

            batchRepository
                .Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(batchEvents);

            var controller = BuildController(sessionService.Object, configuration, batchRepository);

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);

            Assert.Equal(2, model.WeeklyBatchRuns);
            Assert.Equal(15, model.WeeklyBatchRequestedCount);
            Assert.Equal(9, model.WeeklyBatchUpdatedCount);
            Assert.Equal(6, model.WeeklyBatchSkippedCount);
            Assert.Equal(60m, model.WeeklyBatchEffectivenessPct);
            Assert.True(model.IsWeeklyBatchEffectivenessLow);
            Assert.False(model.HasTwoConsecutiveLowWeeks);
        }

        [Fact]
        public async Task Index_ShouldMarkTwoConsecutiveLowWeeks_WhenTrendFallsBelowThresholdTwiceInARow()
        {
            var sessionService = new Mock<ISessionService>();
            var batchRepository = BuildBatchRepositoryMock();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:BatchEffectivenessWarnThresholdPct"] = "70"
                })
                .Build();

            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            sessionService.Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 0));

            var batchEvents = new[]
            {
                new BillingBatchEvent
                {
                    Operation = "MarkPaidBatch",
                    RequestedCount = 10,
                    UpdatedCount = 5,
                    SkippedCount = 5,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
                    ChangedBy = "admin@local"
                },
                new BillingBatchEvent
                {
                    Operation = "MarkPaidBatch",
                    RequestedCount = 8,
                    UpdatedCount = 4,
                    SkippedCount = 4,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    ChangedBy = "admin@local"
                }
            };

            batchRepository
                .Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(batchEvents);

            var controller = BuildController(sessionService.Object, configuration, batchRepository);

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);

            Assert.True(model.HasTwoConsecutiveLowWeeks);
            Assert.Equal(4, model.WeeklyTrendPoints.Count);
        }

        private static BillingController BuildController(ISessionService sessionService, IConfiguration configuration, Mock<IBillingBatchEventRepository>? billingBatchEventRepository = null, Mock<ILogger<BillingController>>? logger = null)
        {
            var resolvedBatchRepository = billingBatchEventRepository ?? BuildBatchRepositoryMock();
            var resolvedLogger = logger ?? new Mock<ILogger<BillingController>>();

            var controller = new BillingController(sessionService, configuration, resolvedBatchRepository.Object, resolvedLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private static Mock<IBillingBatchEventRepository> BuildBatchRepositoryMock()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<BillingBatchEvent>());
            batchRepository
                .Setup(s => s.AddAsync(It.IsAny<BillingBatchEvent>()))
                .Returns(Task.CompletedTask);
            return batchRepository;
        }
    }
}
