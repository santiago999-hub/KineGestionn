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
using KineGestion.Core.DTOs;
using KineGestion.Core.Services;

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

            var result = await ctx.Controller.Index(null);

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
            ctx.SessionService.Setup(s => s.GetDashboardCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>())).ThrowsAsync(new InvalidOperationException("boom"));

            var result = await ctx.Controller.Index(null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(12, model.PacientesActivosCount);
            Assert.Equal(4, model.ProfesionalesActivosCount);
            Assert.Equal(18, model.TratamientosCount);
            Assert.Equal(0, model.SesionesCount);
            Assert.Equal(0, model.SesionesHoyCount);
            Assert.Equal(0, model.SesionesCompletadasHoyCount);
            Assert.Equal(0, model.SesionesCanceladasHoyCount);
            Assert.Equal(0, model.SesionesPendientesPagoCount);
            Assert.Equal(0, model.SesionesPendientesConfirmacionCount);
        }

        [Fact]
        public async Task Index_ShouldReuseCachedDashboard_OnSecondCall()
        {
            var ctx = BuildDefaultContext();

            var firstResult = await ctx.Controller.Index(null);
            var secondResult = await ctx.Controller.Index(null);

            Assert.IsType<ViewResult>(firstResult);
            Assert.IsType<ViewResult>(secondResult);

            // Al estar cacheado, la segunda llamada no debe volver a consultar ningún dato.
            ctx.PatientService.Verify(s => s.CountActiveAsync(), Times.Once);
            ctx.ProfessionalService.Verify(s => s.CountActiveAsync(), Times.Once);
            ctx.TreatmentService.Verify(s => s.CountAsync(), Times.Once);
            ctx.SessionService.Verify(s => s.GetDashboardCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Once);
            ctx.SessionService.Verify(s => s.CountByCancellationReasonInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Once);
            ctx.BillingAlertService.Verify(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default), Times.Once);
            ctx.DispatchEventRepository.Verify(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
            ctx.DispatchEventRepository.Verify(r => r.GetByTypeAsync("BillingBatchLowEffectivenessAlert", null, null, 3), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposeBillingOperationalAlertStatus_WhenDetectedAndSentToday()
        {
            var ctx = BuildDefaultContext();
            ctx.BillingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            ctx.DispatchEventRepository
                .Setup(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);
            ctx.DispatchEventRepository
                .Setup(r => r.GetByTypeAsync("BillingBatchLowEffectivenessAlert", null, null, 3))
                .ReturnsAsync(new[]
                {
                    new DispatchEvent
                    {
                        DispatchType = "BillingBatchLowEffectivenessAlert",
                        SentAtUtc = DateTime.UtcNow.AddMinutes(-5),
                        ChangedBy = "system"
                    }
                });

            var result = await ctx.Controller.Index(null);

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

            ctx.DispatchEventRepository
                .Setup(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);
            ctx.DispatchEventRepository
                .Setup(r => r.GetByTypeAsync("BillingBatchLowEffectivenessAlert", null, null, 3))
                .ReturnsAsync(new[]
                {
                    new DispatchEvent
                    {
                        DispatchType = "BillingBatchLowEffectivenessAlert",
                        SentAtUtc = DateTime.UtcNow.AddMinutes(-3),
                        ChangedBy = "admin@kinegestion.local"
                    }
                });

            var result = await ctx.Controller.Index(null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);
            Assert.Single(model.RecentBillingOperationalAlerts);
            Assert.False(model.RecentBillingOperationalAlerts[0].IsSystemTriggered);
            Assert.Equal("Manual", model.RecentBillingOperationalAlerts[0].TriggerSourceLabel);
        }

        [Fact]
        public async Task Index_ShouldForwardSelectedOffice_ToSessionScopedQueries()
        {
            var ctx = BuildDefaultContext();
            ctx.OfficeService.Setup(s => s.GetActiveAsync()).ReturnsAsync(new[]
            {
                new Office { Id = 7, Name = "Oficina Norte" }
            });

            var result = await ctx.Controller.Index(7);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(7, model.SelectedOfficeId);
            Assert.Equal("Oficina Norte", model.OfficeName);

            ctx.SessionService.Verify(
                s => s.GetDashboardCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()),
                Times.Once);
            ctx.SessionService.Verify(
                s => s.CountByCancellationReasonInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), 7),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldRecomputeDashboard_AfterDomainChangeInvalidatesCache()
        {
            var ctx = BuildDefaultContext();

            await ctx.Controller.Index(null);

            // Una escritura de dominio (alta/edición de sesión, cobro, paciente) invalida la caché.
            QueryCache.InvalidatePrefix("sessions:");

            await ctx.Controller.Index(null);

            ctx.SessionService.Verify(
                s => s.GetDashboardCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()),
                Times.Exactly(2));
            ctx.PatientService.Verify(s => s.CountActiveAsync(), Times.Exactly(2));
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

        [Fact]
        public async Task Index_ShouldExposeTodayAgendaAndChartSeries_ScopedToOffice()
        {
            var ctx = new HomeTestContext();
            ctx.SetupDefaultMetrics();
            ctx.OfficeService.Setup(s => s.GetActiveAsync()).ReturnsAsync(new[]
            {
                new Office { Id = 7, Name = "Oficina Norte" }
            });
            ctx.SessionService
                .Setup(s => s.GetAgendaForDayAsync(It.IsAny<DateTime>(), 7))
                .ReturnsAsync(new[]
                {
                    new SessionAgendaItemDto(11, DateTime.UtcNow.Date.AddHours(9), "Lopez, Maria", "Diaz, Jose", "Oficina Norte", SessionStatus.Pending, PaymentStatus.Pending),
                    new SessionAgendaItemDto(12, DateTime.UtcNow.Date.AddHours(8), "Perez, Ana", "Diaz, Jose", "Oficina Norte", SessionStatus.Completed, PaymentStatus.Paid)
                });
            ctx.SessionService
                .Setup(s => s.GetDailySessionSeriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), 7))
                .ReturnsAsync(new[]
                {
                    new DailySessionCountDto(DateTime.UtcNow.Date, 5, 3, 1, 2)
                });
            ctx.SessionService
                .Setup(s => s.GetStatusDistributionInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), 7))
                .ReturnsAsync(new[]
                {
                    new SessionStatusCountDto(SessionStatus.Completed, 3),
                    new SessionStatusCountDto(SessionStatus.Canceled, 1),
                    new SessionStatusCountDto(SessionStatus.Pending, 1)
                });

            var result = await ctx.Controller.Index(7);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(2, model.TodayAgenda.Count);
            Assert.Equal(12, model.TodayAgenda[0].SessionId);
            Assert.Single(model.DailySessions);
            Assert.Equal(5, model.DailySessions[0].Total);
            Assert.Equal(3, model.StatusDistribution.Count);
            Assert.Equal("Completadas", model.StatusDistribution[0].Label);
            Assert.Equal("good", model.StatusDistribution[0].Tone);

            ctx.SessionService.Verify(s => s.GetAgendaForDayAsync(It.IsAny<DateTime>(), 7), Times.Once);
            ctx.SessionService.Verify(s => s.GetStatusDistributionInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), 7), Times.Once);
        }

        private sealed class HomeTestContext
        {
            public Mock<ILogger<HomeController>> Logger { get; } = new();
            public Mock<IPatientService> PatientService { get; } = new();
            public Mock<IProfessionalService> ProfessionalService { get; } = new();
            public Mock<ITreatmentService> TreatmentService { get; } = new();
            public Mock<ISessionService> SessionService { get; } = new();
            public Mock<IDispatchEventRepository> DispatchEventRepository { get; } = new();
            public Mock<IBillingOperationalAlertService> BillingAlertService { get; } = new();
            public Mock<IOfficeService> OfficeService { get; } = new();
            public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

            public HomeController Controller => new(
                Logger.Object,
                Cache,
                PatientService.Object,
                ProfessionalService.Object,
                TreatmentService.Object,
                SessionService.Object,
                DispatchEventRepository.Object,
                BillingAlertService.Object,
                OfficeService.Object);

            public void SetupDefaultMetrics()
            {
                PatientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
                ProfessionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
                TreatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
                SessionService.Setup(s => s.GetDashboardCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(new DashboardSessionCountsDto(60, 7, 3, 2, 9, 5, 20, 15, 20, 2, 1));
                SessionService.Setup(s => s.CountByCancellationReasonInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>())).ReturnsAsync(new Dictionary<CancellationReason, int>());
                DispatchEventRepository.Setup(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(0);
                DispatchEventRepository.Setup(r => r.GetByTypeAsync("BillingBatchLowEffectivenessAlert", null, null, 3)).ReturnsAsync(Array.Empty<DispatchEvent>());
                BillingAlertService.Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });
                OfficeService.Setup(s => s.GetActiveAsync()).ReturnsAsync(Array.Empty<Office>());
            }
        }
    }
}