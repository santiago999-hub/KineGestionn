using KineGestion.Core.Entities;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Mapping
{
    public static class MappingHelper
    {
        public static SessionViewModel ToSessionViewModel(Session session, bool includeEvolution)
        {
            return new SessionViewModel
            {
                Id = session.Id,
                FechaHora = session.FechaHora,
                Observaciones = session.Observaciones,
                InternalNotes = session.InternalNotes,
                Evolution = includeEvolution ? session.Evolution : null,
                Status = session.Status,
                PaymentStatus = session.PaymentStatus,
                NroSesionEnTratamiento = session.NroSesionEnTratamiento,
                OfficeId = session.OfficeId,
                PacienteId = session.PatientId,
                ProfesionalId = session.ProfessionalId,
                TratamientoId = session.TreatmentId,
                PacienteNombre = session.Patient is null ? string.Empty : $"{session.Patient.Apellido}, {session.Patient.Nombre}",
                ProfesionalNombre = session.Professional is null ? string.Empty : $"{session.Professional.Apellido}, {session.Professional.Nombre}",
                TratamientoDescripcion = session.Treatment?.Descripcion,
                OfficeNombre = session.Office?.Name,
                EvolutionBloqueada = session.EvolutionLockedAt.HasValue,
                CreatedBy = session.CreatedBy,
                UpdatedBy = session.UpdatedBy,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            };
        }

        public static Session ToSessionEntity(SessionViewModel viewModel)
        {
            return new Session
            {
                Id = viewModel.Id,
                FechaHora = viewModel.FechaHora,
                Observaciones = viewModel.Observaciones,
                InternalNotes = viewModel.InternalNotes,
                Evolution = viewModel.Evolution,
                Status = viewModel.Status,
                PaymentStatus = viewModel.PaymentStatus,
                NroSesionEnTratamiento = viewModel.NroSesionEnTratamiento,
                OfficeId = viewModel.OfficeId,
                PatientId = viewModel.PacienteId,
                ProfessionalId = viewModel.ProfesionalId,
                TreatmentId = viewModel.TratamientoId
            };
        }

        public static OfficeViewModel ToOfficeViewModel(Office office)
        {
            return new OfficeViewModel
            {
                Id = office.Id,
                Name = office.Name,
                IsActive = office.IsActive
            };
        }

        public static Office ToOfficeEntity(OfficeViewModel viewModel)
        {
            return new Office
            {
                Id = viewModel.Id,
                Name = viewModel.Name,
                IsActive = viewModel.IsActive
            };
        }
    }
}
