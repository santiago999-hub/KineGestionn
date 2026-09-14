using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;

namespace KineGestion.Web.Tests
{
    public class HomeControllerTests
    {
        private static HomeTestContext BuildDefaultContext()
        {
            var ctx = new HomeTestContext();
            ctx.SetupDefaultMetrics();
            return ctx;
        }

        [Fact]
        public async Task Index_ShouldPopulateAllDashboardMetrics()
        {
            var ctx = BuildDefaultContext();

            var result = await ctx.Controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(12, model.PacientesActivosCount);
            Assert.Equal(4, model.ProfesionalesActivosCount);
            Assert.Equal(18, model.TratamientosCount);
            Assert.Equal(60, model.SesionesCount);
            Assert.Equal(7, model.SesionesHoyCount);
            Assert.Equal(3, model.SesionesCompletadasHoyCount);
            Assert.Equal(2, model.SesionesCanceladasHoyCount);
            Assert.Equal(9, model.SesionesPendientesPagoCount);
            Assert.Equal(5, model.SesionesPendientesConfirmacionCount);
        }

        [Fact]
        public async Task Index_ShouldReturnZeroForMetric_WhenAServiceFails()
        {
            var ctx = BuildDefaultContext();
            ctx.SessionService.Setup(s => s.CountAsync()).ThrowsAsync(new InvalidOperationException("boom"));

            var result = await ctx.Controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(12, model.PacientesActivosCount);
            Assert.Equal(4, model.ProfesionalesActivosCount);
            Assert.Equal(18, model.TratamientosCount);
            Assert.Equal(0, model.SesionesCount);
            Assert.Equal(7, model.SesionesHoyCount);
            Assert.Equal(3, model.SesionesCompletadasHoyCount);
            Assert.Equal(2, model.SesionesCanceladasHoyCount);
            Assert.Equal(9, model.SesionesPendientesPagoCount);
            Assert.Equal(5, model.SesionesPendientesConfirmacionCount);
        }

        [Fact]
        public async Task Index_ShouldReuseCachedDashboard_OnSecondCall()
        {
            var ctx = BuildDefaultContext();

            var firstResult = await ctx.Controller.Index();
            var secondResult = await ctx.Controller.Index();

            Assert.IsType<ViewResult>(firstResult);
            Assert.IsType<ViewResult>(secondResult);

            // Al estar cacheado, la segunda llamada no debe volver a consultar ningún dato.
            ctx.PatientService.Verify(s => s.CountActiveAsync(), Times.Once);
            ctx.ProfessionalService.Verify(s => s.CountActiveAsync(), Times.Once);
            ctx.TreatmentService.Verify(s => s.CountAsync(), Times.Once);
            ctx.SessionService.Verify(s => s.CountAsync(), Times.Once);
            ctx.SessionService.Verify(s => s.CountTodayAsync(It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusOnDateAsync(SessionStatus.Canceled, It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusAndPaymentStatusAsync(SessionStatus.Completed, PaymentStatus.Pending), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusAsync(SessionStatus.Pending), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusInRangeAsync(SessionStatus.Completed, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByCancellationReasonInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            ctx.BillingAlertService.Verify(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default), Times.Once);
            ctx.AuditLogService.Verify(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 1), Times.Once);
            ctx.AuditLogService.Verify(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposeBillingOperationalAlertStatus_WhenDetectedAndSentToday()
        {
            var ctx = BuildDefaultContext();
            ctx.BillingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            ctx.AuditLogService
                .Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 1))
                .ReturnsAsync((new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow } }.AsEnumerable(), 1));
            ctx.AuditLogService
                .Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3))
                .ReturnsAsync((new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow.AddMinutes(-5), ChangedBy = "system" } }.AsEnumerable(), 1));

            var result = await ctx.Controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);
            Assert.True(model.IsBillingOperationalAlertActive);
            Assert.True(model.IsBillingOperationalAlertSentToday);
            Assert.True(model.LastBillingOperationalAlertAtUtc.HasValue);
            Assert.Equal("system", model.LastBillingOperationalAlertChangedBy);
            Assert.Single(model.RecentBillingOperationalAlerts);
            Assert.True(model.RecentBillingOperationalAlerts[0].IsSystemTriggered);
            Assert.Equal("System", model.RecentBillingOperationalAlerts[0].TriggerSourceLabel);
        }

        [Fact]
        public async Task Index_ShouldClassifyRecentBillingOperationalAlertAsManual_WhenChangedByIsUser()
        {
            var ctx = BuildDefaultContext();
            ctx.BillingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            ctx.AuditLogService
                .Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 1))
                .ReturnsAsync((new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow } }.AsEnumerable(), 1));
            ctx.AuditLogService
                .Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3))
                .ReturnsAsync((new[]
                {
                    new AuditLog
                    {
                        EntityName = "OperationalAlert",
                        Action = "Create",
                        ChangedAt = DateTime.UtcNow.AddMinutes(-3),
                        ChangedBy = "admin@kinegestion.local"
                    }
                }.AsEnumerable(), 1));

            var result = await ctx.Controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);
            Assert.Single(model.RecentBillingOperationalAlerts);
            Assert.False(model.RecentBillingOperationalAlerts[0].IsSystemTriggered);
            Assert.Equal("Manual", model.RecentBillingOperationalAlerts[0].TriggerSourceLabel);
        }

        [Fact]
        public async Task TriggerBillingOperationalAlert_ShouldSetSuccess_WhenQueued()
        {
            var ctx = new HomeTestContext();
            ctx.BillingAlertService
                .Setup(s => s.QueueAlertIfNeededAsync(It.IsAny<string?>(), It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertDispatchResult { Queued = true, Message = "Alerta operativa de cobranzas encolada para administración." });

            var controller = ctx.Controller;
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

            var result = await controller.TriggerBillingOperationalAlert();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Alerta operativa de cobranzas encolada para administración.", controller.TempData["Success"]);
        }

        private sealed class HomeTestContext
        {
            public Mock<ILogger<HomeController>> Logger { get; } = new();
            public Mock<IPatientService> PatientService { get; } = new();
            public Mock<IProfessionalService> ProfessionalService { get; } = new();
            public Mock<ITreatmentService> TreatmentService { get; } = new();
            public Mock<ISessionService> SessionService { get; } = new();
            public Mock<IAuditLogService> AuditLogService { get; } = new();
            public Mock<IBillingOperationalAlertService> BillingAlertService { get; } = new();
            public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

            public HomeController Controller => new(
                Logger.Object,
                Cache,
                PatientService.Object,
                ProfessionalService.Object,
                TreatmentService.Object,
                SessionService.Object,
                AuditLogService.Object,
                BillingAlertService.Object);

            public void SetupDefaultMetrics()
            {
                PatientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
                ProfessionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
                TreatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
                SessionService.Setup(s => s.CountAsync()).ReturnsAsync(60);
                SessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
                SessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
                SessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Canceled, It.IsAny<DateTime>())).ReturnsAsync(2);
                SessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(SessionStatus.Completed, PaymentStatus.Pending)).ReturnsAsync(9);
                SessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
                SessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Completed, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
                SessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(15);
                SessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
                SessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
                SessionService.Setup(s => s.CountByCancellationReasonInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new Dictionary<CancellationReason, int>());
                AuditLogService.Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 1)).ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));
                AuditLogService.Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3)).ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));
                BillingAlertService.Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });
            }
        }
    }
}