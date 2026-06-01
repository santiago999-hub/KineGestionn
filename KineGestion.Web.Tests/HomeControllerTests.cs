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

namespace KineGestion.Web.Tests
{
    public class HomeControllerTests
    {
        [Fact]
        public async Task Index_ShouldPopulateAllDashboardMetrics()
        {
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(60);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Canceled, It.IsAny<DateTime>())).ReturnsAsync(2);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(SessionStatus.Completed, PaymentStatus.Pending)).ReturnsAsync(9);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Completed, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(15);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
            auditLogService.Setup(s => s.GetAllAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(Array.Empty<AuditLog>());
            auditLogService.Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3)).ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));
            billingAlertService.Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object);

            var result = await controller.Index();

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
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ThrowsAsync(new InvalidOperationException("boom"));
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Canceled, It.IsAny<DateTime>())).ReturnsAsync(2);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(SessionStatus.Completed, PaymentStatus.Pending)).ReturnsAsync(9);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Completed, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(15);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
            auditLogService.Setup(s => s.GetAllAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(Array.Empty<AuditLog>());
            auditLogService.Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3)).ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));
            billingAlertService.Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object);

            var result = await controller.Index();

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
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(60);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Canceled, It.IsAny<DateTime>())).ReturnsAsync(2);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(SessionStatus.Completed, PaymentStatus.Pending)).ReturnsAsync(9);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Completed, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(15);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
            auditLogService.Setup(s => s.GetAllAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(Array.Empty<AuditLog>());
            auditLogService.Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3)).ReturnsAsync((Array.Empty<AuditLog>().AsEnumerable(), 0));
            billingAlertService.Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = false });

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object);

            var firstResult = await controller.Index();
            var secondResult = await controller.Index();

            Assert.IsType<ViewResult>(firstResult);
            Assert.IsType<ViewResult>(secondResult);
            patientService.Verify(s => s.CountActiveAsync(), Times.Once);
            professionalService.Verify(s => s.CountActiveAsync(), Times.Once);
            treatmentService.Verify(s => s.CountAsync(), Times.Once);
            sessionService.Verify(s => s.CountAsync(), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposeBillingOperationalAlertStatus_WhenDetectedAndSentToday()
        {
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(1);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(1);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(1);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(1);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(It.IsAny<SessionStatus>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(It.IsAny<SessionStatus>(), It.IsAny<PaymentStatus>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAsync(It.IsAny<SessionStatus>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(It.IsAny<SessionStatus>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(It.IsAny<SessionStatus>(), It.IsAny<PaymentStatus>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);

            billingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            auditLogService
                .Setup(s => s.GetAllAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow } });
            auditLogService
                .Setup(s => s.GetPagedAsync("OperationalAlert", null, null, "Create", null, null, 1, 3))
                .ReturnsAsync((new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow.AddMinutes(-5), ChangedBy = "system" } }.AsEnumerable(), 1));

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object);

            var result = await controller.Index();

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
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(1);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(1);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(1);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(1);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(It.IsAny<SessionStatus>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusAsync(It.IsAny<SessionStatus>(), It.IsAny<PaymentStatus>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAsync(It.IsAny<SessionStatus>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(It.IsAny<SessionStatus>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(It.IsAny<SessionStatus>(), It.IsAny<PaymentStatus>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);

            billingAlertService
                .Setup(s => s.GetSnapshotAsync(It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertSnapshot { ThresholdPct = 70m, HasConsecutiveLowWeeks = true });

            auditLogService
                .Setup(s => s.GetAllAsync("OperationalAlert", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create", ChangedAt = DateTime.UtcNow } });
            auditLogService
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

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);
            Assert.Single(model.RecentBillingOperationalAlerts);
            Assert.False(model.RecentBillingOperationalAlerts[0].IsSystemTriggered);
            Assert.Equal("Manual", model.RecentBillingOperationalAlerts[0].TriggerSourceLabel);
        }

        [Fact]
        public async Task TriggerBillingOperationalAlert_ShouldSetSuccess_WhenQueued()
        {
            var logger = new Mock<ILogger<HomeController>>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var sessionService = new Mock<ISessionService>();
            var auditLogService = new Mock<IAuditLogService>();
            var billingAlertService = new Mock<IBillingOperationalAlertService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            billingAlertService
                .Setup(s => s.QueueAlertIfNeededAsync(It.IsAny<string?>(), It.IsAny<DateTime>(), default))
                .ReturnsAsync(new BillingOperationalAlertDispatchResult { Queued = true, Message = "Alerta operativa de cobranzas encolada para administración." });

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object,
                auditLogService.Object,
                billingAlertService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

            var result = await controller.TriggerBillingOperationalAlert();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Alerta operativa de cobranzas encolada para administración.", controller.TempData["Success"]);
        }
    }
}