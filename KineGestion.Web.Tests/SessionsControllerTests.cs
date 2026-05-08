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
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KineGestion.Web.Tests
{
    public class SessionsControllerTests
    {
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
