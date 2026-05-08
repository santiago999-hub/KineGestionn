using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace KineGestion.Web.Tests
{
    public class SessionsControllerTests
    {
        [Fact]
        public async Task Index_ShouldForwardDateFiltersAndPopulateModel()
        {
            var sessionService = new Mock<ISessionService>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var officeService = new Mock<IOfficeService>();

            var dateFrom = new DateTime(2026, 5, 8);
            var dateTo = new DateTime(2026, 5, 8);

            sessionService
                .Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    "Perez",
                    SessionStatus.Pending,
                    PaymentStatus.Pending,
                    dateFrom,
                    dateTo,
                    "fecha",
                    "desc"))
                .ReturnsAsync((
                    new[]
                    {
                        new SessionListDto(
                            15,
                            new DateTime(2026, 5, 8, 10, 30, 0),
                            SessionStatus.Pending,
                            PaymentStatus.Pending,
                            2,
                            "Perez, Juan",
                            "Gomez, Ana",
                            "Rehabilitación",
                            "Consultorio 1",
                            false)
                    },
                    1));

            var controller = new SessionsController(
                sessionService.Object,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                officeService.Object);

            var result = await controller.Index("Perez", SessionStatus.Pending, PaymentStatus.Pending, dateFrom, dateTo, "fecha", "desc", 1, 10);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SessionIndexViewModel>(view.Model);

            Assert.Single(model.Items);
            Assert.Equal(dateFrom, model.DateFrom);
            Assert.Equal(dateTo, model.DateTo);
            Assert.Equal(SessionStatus.Pending, model.Status);
            Assert.Equal(PaymentStatus.Pending, model.PaymentStatus);
            Assert.Equal(1, model.TotalCount);
        }

        [Fact]
        public async Task Create_ShouldAddModelStateError_WhenSessionServiceThrowsBusinessValidationException()
        {
            var sessionService = new Mock<ISessionService>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var officeService = new Mock<IOfficeService>();

            SetupSelectLists(patientService, professionalService, treatmentService, officeService);

            const string message = "La numeración de sesión se actualizó por concurrencia. Reintentá guardar para asignar el próximo número disponible.";
            sessionService
                .Setup(s => s.CreateAsync(It.IsAny<Session>()))
                .ThrowsAsync(new BusinessValidationException(message, nameof(Session.NroSesionEnTratamiento)));

            var controller = new SessionsController(
                sessionService.Object,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                officeService.Object);

            var vm = BuildValidViewModel();

            var result = await controller.Create(vm);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(vm, view.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(Session.NroSesionEnTratamiento)));
            Assert.Contains(message, controller.ModelState[nameof(Session.NroSesionEnTratamiento)]!.Errors.Select(e => e.ErrorMessage));
        }

        [Fact]
        public async Task Edit_ShouldAddModelStateError_WhenSessionServiceThrowsBusinessValidationException()
        {
            var sessionService = new Mock<ISessionService>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var officeService = new Mock<IOfficeService>();

            SetupSelectLists(patientService, professionalService, treatmentService, officeService);

            const string message = "No se pudo guardar la sesión por concurrencia alta. Por favor, intentá nuevamente.";
            sessionService
                .Setup(s => s.UpdateAsync(It.IsAny<Session>()))
                .ThrowsAsync(new BusinessValidationException(message, nameof(Session.NroSesionEnTratamiento)));

            var controller = new SessionsController(
                sessionService.Object,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                officeService.Object);

            var vm = BuildValidViewModel();
            vm.Id = 15;

            var result = await controller.Edit(15, vm);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(vm, view.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(Session.NroSesionEnTratamiento)));
            Assert.Contains(message, controller.ModelState[nameof(Session.NroSesionEnTratamiento)]!.Errors.Select(e => e.ErrorMessage));
        }

        [Fact]
        public async Task MyAgenda_ShouldForwardDateFiltersAndPopulateModel()
        {
            var sessionService = new Mock<ISessionService>();
            var patientService = new Mock<IPatientService>();
            var professionalService = new Mock<IProfessionalService>();
            var treatmentService = new Mock<ITreatmentService>();
            var officeService = new Mock<IOfficeService>();

            var dateFrom = new DateTime(2026, 5, 8);
            var dateTo = new DateTime(2026, 5, 10);

            sessionService
                .Setup(s => s.GetPagedListByProfessionalAsync(
                    7,
                    1,
                    10,
                    "Perez",
                    SessionStatus.Completed,
                    PaymentStatus.Paid,
                    dateFrom,
                    dateTo))
                .ReturnsAsync((
                    new[]
                    {
                        new SessionListDto(
                            21,
                            new DateTime(2026, 5, 9, 11, 0, 0),
                            SessionStatus.Completed,
                            PaymentStatus.Paid,
                            4,
                            "Perez, Juan",
                            string.Empty,
                            "Rehabilitación",
                            "Consultorio 2",
                            false)
                    },
                    1));

            var controller = new SessionsController(
                sessionService.Object,
                patientService.Object,
                professionalService.Object,
                treatmentService.Object,
                officeService.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("ProfessionalId", "7")
                    }, "TestAuth"))
                }
            };

            var result = await controller.MyAgenda("Perez", SessionStatus.Completed, PaymentStatus.Paid, dateFrom, dateTo, 1, 10);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SessionIndexViewModel>(view.Model);

            Assert.Single(model.Items);
            Assert.Equal(dateFrom, model.DateFrom);
            Assert.Equal(dateTo, model.DateTo);
            Assert.Equal(SessionStatus.Completed, model.Status);
            Assert.Equal(PaymentStatus.Paid, model.PaymentStatus);
            Assert.Equal(1, model.TotalCount);
        }

        private static SessionViewModel BuildValidViewModel() => new()
        {
            Id = 1,
            FechaHora = DateTime.UtcNow,
            PacienteId = 1,
            ProfesionalId = 1,
            TratamientoId = 1,
            NroSesionEnTratamiento = 1,
            Status = SessionStatus.Pending,
            PaymentStatus = PaymentStatus.Pending
        };

        private static void SetupSelectLists(
            Mock<IPatientService> patientService,
            Mock<IProfessionalService> professionalService,
            Mock<ITreatmentService> treatmentService,
            Mock<IOfficeService> officeService)
        {
            patientService
                .Setup(s => s.GetForSelectAsync())
                .ReturnsAsync(new List<PatientSelectDto>());

            professionalService
                .Setup(s => s.GetForSelectAsync())
                .ReturnsAsync(new List<ProfessionalSelectDto>());

            treatmentService
                .Setup(s => s.GetForSelectAsync())
                .ReturnsAsync(new List<TreatmentSelectDto>());

            officeService
                .Setup(s => s.GetActiveAsync())
                .ReturnsAsync(new List<Office>());
        }
    }
}
