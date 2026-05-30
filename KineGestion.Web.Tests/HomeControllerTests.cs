using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(60);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    null,
                    null,
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 9));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    null,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 20));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Paid,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 15));

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(12, model.PacientesActivosCount);
            Assert.Equal(4, model.ProfesionalesActivosCount);
            Assert.Equal(18, model.TratamientosCount);
            Assert.Equal(60, model.SesionesCount);
            Assert.Equal(7, model.SesionesHoyCount);
            Assert.Equal(3, model.SesionesCompletadasHoyCount);
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ThrowsAsync(new InvalidOperationException("boom"));
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    null,
                    null,
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 9));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    null,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 20));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Paid,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 15));

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<HomeDashboardViewModel>(view.Model);

            Assert.Equal(12, model.PacientesActivosCount);
            Assert.Equal(4, model.ProfesionalesActivosCount);
            Assert.Equal(18, model.TratamientosCount);
            Assert.Equal(0, model.SesionesCount);
            Assert.Equal(7, model.SesionesHoyCount);
            Assert.Equal(3, model.SesionesCompletadasHoyCount);
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            patientService.Setup(s => s.CountActiveAsync()).ReturnsAsync(12);
            professionalService.Setup(s => s.CountActiveAsync()).ReturnsAsync(4);
            treatmentService.Setup(s => s.CountAsync()).ReturnsAsync(18);
            sessionService.Setup(s => s.CountAsync()).ReturnsAsync(60);
            sessionService.Setup(s => s.CountTodayAsync(It.IsAny<DateTime>())).ReturnsAsync(7);
            sessionService.Setup(s => s.CountByStatusOnDateAsync(SessionStatus.Completed, It.IsAny<DateTime>())).ReturnsAsync(3);
            sessionService.Setup(s => s.CountByStatusAsync(SessionStatus.Pending)).ReturnsAsync(5);
            sessionService.Setup(s => s.CountInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(20);
            sessionService.Setup(s => s.CountByStatusInRangeAsync(SessionStatus.Canceled, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    null,
                    null,
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 9));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    null,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 20));
            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    1,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Paid,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((Enumerable.Empty<SessionListDto>(), 15));

            var controller = new HomeController(
                logger.Object,
                memoryCache,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                sessionService.Object);

            var firstResult = await controller.Index();
            var secondResult = await controller.Index();

            Assert.IsType<ViewResult>(firstResult);
            Assert.IsType<ViewResult>(secondResult);
            patientService.Verify(s => s.CountActiveAsync(), Times.Once);
            professionalService.Verify(s => s.CountActiveAsync(), Times.Once);
            treatmentService.Verify(s => s.CountAsync(), Times.Once);
            sessionService.Verify(s => s.CountAsync(), Times.Once);
        }
    }
}