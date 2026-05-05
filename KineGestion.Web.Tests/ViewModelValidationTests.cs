using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Tests
{
    public class ViewModelValidationTests
    {
        // ─── TreatmentViewModel ───────────────────────────────────────────────────

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

        // ─── SessionViewModel ─────────────────────────────────────────────────────

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

        // ─── UserViewModel ────────────────────────────────────────────────────────

        [Fact]
        public void UserViewModel_ShouldFail_WhenEmailIsEmpty()
        {
            var model = new UserViewModel { Email = "", Rol = "Admin" };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserViewModel.Email)));
        }

        [Fact]
        public void UserViewModel_ShouldFail_WhenEmailFormatIsInvalid()
        {
            var model = new UserViewModel { Email = "no-es-un-email", Rol = "Admin" };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserViewModel.Email)));
        }

        [Fact]
        public void UserViewModel_ShouldFail_WhenRolIsEmpty()
        {
            var model = new UserViewModel { Email = "admin@test.com", Rol = "" };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserViewModel.Rol)));
        }

        [Fact]
        public void UserViewModel_ShouldFail_WhenPasswordIsTooShort()
        {
            var model = new UserViewModel { Email = "admin@test.com", Rol = "Admin", Password = "corta" };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserViewModel.Password)));
        }

        [Fact]
        public void UserViewModel_ShouldPass_WhenDataIsValid()
        {
            var model = new UserViewModel { Email = "admin@test.com", Rol = "Admin" };

            var errors = Validate(model);

            Assert.Empty(errors);
        }

        // ─── PatientViewModel ─────────────────────────────────────────────────────

        [Fact]
        public void PatientViewModel_ShouldFail_WhenDniHasLetters()
        {
            var model = BuildValidPatient();
            model.DNI = "ABC12345";

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PatientViewModel.DNI)));
        }

        [Fact]
        public void PatientViewModel_ShouldFail_WhenDniIsTooShort()
        {
            var model = BuildValidPatient();
            model.DNI = "12345"; // menos de 7 dígitos

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PatientViewModel.DNI)));
        }

        [Fact]
        public void PatientViewModel_ShouldFail_WhenNombreIsEmpty()
        {
            var model = BuildValidPatient();
            model.Nombre = "";

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PatientViewModel.Nombre)));
        }

        [Fact]
        public void PatientViewModel_ShouldPass_WhenDataIsValid()
        {
            var model = BuildValidPatient();

            var errors = Validate(model);

            Assert.Empty(errors);
        }

        // ─── OfficeViewModel ──────────────────────────────────────────────────────

        [Fact]
        public void OfficeViewModel_ShouldFail_WhenNameIsEmpty()
        {
            var model = new OfficeViewModel { Name = "" };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(OfficeViewModel.Name)));
        }

        [Fact]
        public void OfficeViewModel_ShouldFail_WhenNameExceedsMaxLength()
        {
            var model = new OfficeViewModel { Name = new string('A', 101) }; // límite es 100

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(OfficeViewModel.Name)));
        }

        // ─── ProfessionalViewModel ────────────────────────────────────────────────

        [Fact]
        public void ProfessionalViewModel_ShouldFail_WhenMatriculaHasInvalidChars()
        {
            var model = BuildValidProfessional();
            model.Matricula = "MP 123!"; // espacios y ! no permitidos por el regex

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ProfessionalViewModel.Matricula)));
        }

        [Fact]
        public void ProfessionalViewModel_ShouldFail_WhenEspecialidadIsEmpty()
        {
            var model = BuildValidProfessional();
            model.Especialidad = "";

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ProfessionalViewModel.Especialidad)));
        }

        [Fact]
        public void ProfessionalViewModel_ShouldPass_WhenDataIsValid()
        {
            var model = BuildValidProfessional();

            var errors = Validate(model);

            Assert.Empty(errors);
        }

        // ─── TreatmentViewModel extra ─────────────────────────────────────────────

        [Fact]
        public void TreatmentViewModel_ShouldFail_WhenCantidadSesionesIsZero()
        {
            var model = new TreatmentViewModel
            {
                Descripcion = "Plan",
                CantidadSesionesTotales = 0, // mínimo 1
                FechaInicio = DateTime.Today,
                PacienteId = 1
            };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(TreatmentViewModel.CantidadSesionesTotales)));
        }

        [Fact]
        public void TreatmentViewModel_ShouldFail_WhenDescripcionIsEmpty()
        {
            var model = new TreatmentViewModel
            {
                Descripcion = "",
                CantidadSesionesTotales = 10,
                FechaInicio = DateTime.Today,
                PacienteId = 1
            };

            var errors = Validate(model);

            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(TreatmentViewModel.Descripcion)));
        }

        // ─── helpers ─────────────────────────────────────────────────────────────

        private static PatientViewModel BuildValidPatient() => new()
        {
            Nombre = "Ana",
            Apellido = "Perez",
            DNI = "30111222",
            FechaNacimiento = DateTime.Today.AddYears(-30)
        };

        private static ProfessionalViewModel BuildValidProfessional() => new()
        {
            Nombre = "Laura",
            Apellido = "Diaz",
            Matricula = "MP-1234",
            Especialidad = "Deportiva"
        };

        private static List<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();

            Validator.TryValidateObject(model, context, results, validateAllProperties: true);

            return results;
        }
    }
}