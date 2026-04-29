using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _repository;
        private readonly ITreatmentRepository _treatmentRepository;

        public SessionService(ISessionRepository repository, ITreatmentRepository treatmentRepository)
        {
            _repository = repository;
            _treatmentRepository = treatmentRepository;
        }

        public async Task<Session?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Session>> GetAllForAdminAsync()
        {
            var sessions = await _repository.GetAllAsync();
            foreach (var s in sessions)
                s.Evolution = null;
            return sessions;
        }

        public async Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            string? sortBy,
            string? sortDir)
        {
            var (sessions, totalCount) = await _repository.GetPagedForAdminAsync(page, pageSize, search, status, paymentStatus, sortBy, sortDir);
            foreach (var s in sessions)
                s.Evolution = null;
            return (sessions, totalCount);
        }

        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<int> CountAsync()
            => await _repository.CountAsync();

        public async Task<Session> CreateAsync(Session session)
        {
            await ValidateProfessionalAvailabilityAsync(session.ProfessionalId, session.FechaHora);

            int sesionesExistentes = await _repository.CountByTreatmentIdAsync(session.TreatmentId);

            var treatment = await _treatmentRepository.GetByIdAsync(session.TreatmentId);
            if (treatment is not null && sesionesExistentes >= treatment.CantidadSesionesTotales)
            {
                throw new BusinessValidationException(
                    $"El tratamiento ya alcanzó el límite de {treatment.CantidadSesionesTotales} sesiones.",
                    nameof(Session.TreatmentId));
            }

            session.NroSesionEnTratamiento = sesionesExistentes + 1;
            return await _repository.AddAsync(session);
        }

        public async Task<Session> UpdateAsync(Session session)
        {
            await ValidateProfessionalAvailabilityAsync(session.ProfessionalId, session.FechaHora, session.Id);

            // Si cambió el tratamiento, recalcular el número de sesión en el nuevo tratamiento
            var original = await _repository.GetByIdAsync(session.Id);
            if (original is not null && original.TreatmentId != session.TreatmentId)
            {
                var newTreatment = await _treatmentRepository.GetByIdAsync(session.TreatmentId);
                int sesionesEnNuevoTratamiento = await _repository.CountByTreatmentIdAsync(session.TreatmentId);

                if (newTreatment is not null && sesionesEnNuevoTratamiento >= newTreatment.CantidadSesionesTotales)
                {
                    throw new BusinessValidationException(
                        $"El tratamiento seleccionado ya alcanzó el límite de {newTreatment.CantidadSesionesTotales} sesiones.",
                        nameof(Session.TreatmentId));
                }

                session.NroSesionEnTratamiento = sesionesEnNuevoTratamiento + 1;
            }

            // Inmutabilidad: si la evolución estaba bloqueada, no se puede modificar
            if (original is not null && original.EvolutionLockedAt.HasValue
                && original.Evolution != session.Evolution)
            {
                throw new BusinessValidationException(
                    "La evolución clínica está firmada y no puede modificarse.",
                    nameof(Session.Evolution));
            }

            // Si se acaba de escribir la evolución por primera vez, bloquearla
            if (original is not null && !original.EvolutionLockedAt.HasValue
                && !string.IsNullOrWhiteSpace(session.Evolution))
            {
                session.EvolutionLockedAt = DateTime.UtcNow;
            }
            else if (original is not null)
            {
                session.EvolutionLockedAt = original.EvolutionLockedAt;
            }

            return await _repository.UpdateAsync(session);
        }

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);

        private async Task ValidateProfessionalAvailabilityAsync(int professionalId, DateTime fechaHora, int? excludeSessionId = null)
        {
            bool hasConflict = await _repository.ExistsProfessionalConflictAsync(
                professionalId,
                fechaHora,
                windowInMinutes: 45,
                excludeSessionId: excludeSessionId);

            if (hasConflict)
            {
                throw new BusinessValidationException(
                    "El profesional ya tiene una sesion asignada en un rango de +/- 45 minutos para el horario seleccionado.",
                    nameof(Session.FechaHora));
            }
        }
    }
}
