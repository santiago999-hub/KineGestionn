using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Tests
{
    public class ViewModelValidationTests
    {
        [Fact]
        public void TreatmentViewModel_ShouldRequirePacienteIdGreaterThanZero()
        {
            var model = new TreatmentViewModel
            {
                Descripcion = "Plan de rehabilitacion",
                CantidadSesionesTotales = 10,
                FechaInicio = DateTime.Today,
                PacienteId = 0
            };

            var errors = Validate(model);

            Assert.Contains(errors, error => error.MemberNames.Contains(nameof(TreatmentViewModel.PacienteId)));
        }

        [Fact]
        public void SessionViewModel_ShouldRequireSelectedEntitiesGreaterThanZero()
        {
            var model = new SessionViewModel
            {
                FechaHora = DateTime.Now.AddHours(1),
                PacienteId = 0,
                ProfesionalId = 0,
                TratamientoId = 0,
                NroSesionEnTratamiento = 1
            };

            var errors = Validate(model);

            Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SessionViewModel.PacienteId)));
            Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SessionViewModel.ProfesionalId)));
            Assert.Contains(errors, error => error.MemberNames.Contains(nameof(SessionViewModel.TratamientoId)));
        }

        private static List<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();

            Validator.TryValidateObject(model, context, results, validateAllProperties: true);

            return results;
        }
    }
}