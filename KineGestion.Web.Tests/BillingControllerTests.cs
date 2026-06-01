using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            var auditLogService = BuildAuditLogServiceMock();
            sessionService
                .Setup(s => s.MarkCompletedPendingAsPaidBatchAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync((2, 0));

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build(), auditLogService);

            var result = await controller.MarkPaidBatch(new List<int> { 7, 7, 0, -2, 9 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("2 sesiones marcadas como pagadas.", controller.TempData["Success"]);
            sessionService.Verify(
                s => s.MarkCompletedPendingAsPaidBatchAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(7) && ids.Contains(9))),
                Times.Once);
            auditLogService.Verify(
                s => s.AddAsync(It.Is<AuditLog>(a => a.EntityName == "BillingBatch" && a.Action == "Create" && a.NewValuesJson != null && a.NewValuesJson.Contains("MarkPaidBatch"))),
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
            var auditLogService = BuildAuditLogServiceMock();
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

            var controller = BuildController(sessionService.Object, configuration, auditLogService);
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
        public async Task Index_ShouldAggregateWeeklyBatchMetrics_FromAuditLogEntries()
        {
            var sessionService = new Mock<ISessionService>();
            var auditLogService = BuildAuditLogServiceMock();
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

            var auditItems = new[]
            {
                new AuditLog
                {
                    EntityName = "BillingBatch",
                    Action = "Create",
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = "admin@local",
                    NewValuesJson = "{\"RequestedCount\":10,\"UpdatedCount\":7,\"SkippedCount\":3}"
                },
                new AuditLog
                {
                    EntityName = "BillingBatch",
                    Action = "Create",
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = "admin@local",
                    NewValuesJson = "{\"RequestedCount\":5,\"UpdatedCount\":2,\"SkippedCount\":3}"
                }
            };

            auditLogService
                .Setup(s => s.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(auditItems);

            var controller = BuildController(sessionService.Object, configuration, auditLogService);

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
            var auditLogService = BuildAuditLogServiceMock();
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

            var auditItems = new[]
            {
                new AuditLog
                {
                    EntityName = "BillingBatch",
                    Action = "Create",
                    ChangedAt = DateTime.UtcNow.AddDays(-8),
                    ChangedBy = "admin@local",
                    NewValuesJson = "{\"RequestedCount\":10,\"UpdatedCount\":5,\"SkippedCount\":5}"
                },
                new AuditLog
                {
                    EntityName = "BillingBatch",
                    Action = "Create",
                    ChangedAt = DateTime.UtcNow.AddDays(-1),
                    ChangedBy = "admin@local",
                    NewValuesJson = "{\"RequestedCount\":8,\"UpdatedCount\":4,\"SkippedCount\":4}"
                }
            };

            auditLogService
                .Setup(s => s.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(auditItems);

            var controller = BuildController(sessionService.Object, configuration, auditLogService);

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);

            Assert.True(model.HasTwoConsecutiveLowWeeks);
            Assert.Equal(4, model.WeeklyTrendPoints.Count);
        }

        private static BillingController BuildController(ISessionService sessionService, IConfiguration configuration, Mock<IAuditLogService>? auditLogService = null, Mock<ILogger<BillingController>>? logger = null)
        {
            var resolvedAuditLogService = auditLogService ?? BuildAuditLogServiceMock();
            var resolvedLogger = logger ?? new Mock<ILogger<BillingController>>();

            var controller = new BillingController(sessionService, configuration, resolvedAuditLogService.Object, resolvedLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private static Mock<IAuditLogService> BuildAuditLogServiceMock()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(s => s.GetAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Enumerable.Empty<AuditLog>());
            auditLogService
                .Setup(s => s.AddAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync((AuditLog entry) => entry);
            return auditLogService;
        }
    }
}
