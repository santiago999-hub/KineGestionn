using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _repository;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly int _professionalConflictWindowMinutes;

        public SessionService(
            ISessionRepository repository,
            ITreatmentRepository treatmentRepository,
            int professionalConflictWindowMinutes = 45)
        {
            _repository = repository;
            _treatmentRepository = treatmentRepository;
            _professionalConflictWindowMinutes = professionalConflictWindowMinutes > 0
                ? professionalConflictWindowMinutes
                : 45;
        }

        public async Task<Session?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        /// <summary>OBSOLETO: borra Evolution pero sigue cargando todas las sesiones en memoria. Ver interfaz.</summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync.")]
        public async Task<IEnumerable<Session>> GetAllForAdminAsync()
        {
            var sessions = await _repository.GetAllAsync();
            foreach (var s in sessions)
                s.Evolution = null;
            return sessions;
        }

        /// <summary>OBSOLETO: delega al método obsoleto del repo. Usar GetPagedListForAdminAsync.</summary>
        [Obsolete("Carga entidades completas con 4 JOINs. Usar GetPagedListForAdminAsync.")]
        public async Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? sortBy,
            string? sortDir)
        {
            var (sessions, totalCount) = await _repository.GetPagedForAdminAsync(page, pageSize, search, status, paymentStatus, dateFrom, dateTo, sortBy, sortDir);
            foreach (var s in sessions)
                s.Evolution = null;
            return (sessions, totalCount);
        }

        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(
            int page, int pageSize, string? search,
            SessionStatus? status, PaymentStatus? paymentStatus,
            DateTime? dateFrom, DateTime? dateTo,
            string? sortBy, string? sortDir)
            => await _repository.GetPagedListForAdminAsync(page, pageSize, search, status, paymentStatus, dateFrom, dateTo, sortBy, sortDir);

        [Obsolete("Carga entidades completas con 3 JOINs. Usar GetPagedListByProfessionalAsync.")]
        public async Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedByProfessionalAsync(
            int professionalId,
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus)
        {
            // No se borra Evolution: el profesional ve sus propias evoluciones
#pragma warning disable CS0618
            return await _repository.GetPagedByProfessionalAsync(professionalId, page, pageSize, search, status, paymentStatus);
#pragma warning restore CS0618
        }

        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(
            int professionalId, int page, int pageSize, string? search,
            SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo)
            => await _repository.GetPagedListByProfessionalAsync(professionalId, page, pageSize, search, status, paymentStatus, dateFrom, dateTo);

        /// <summary>OBSOLETO: carga todas las sesiones sin filtro. Ver interfaz para detalles.</summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync o GetPagedListByProfessionalAsync.")]
        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId)
            => await _repository.GetByProfessionalIdAsync(professionalId);

        public async Task<int> CountAsync()
            => await _repository.CountAsync();

        public async Task<int> CountByTreatmentIdAsync(int treatmentId)
            => await _repository.CountByTreatmentIdAsync(treatmentId);

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await _repository.CountByPatientIdAsync(patientId);

        public async Task<int> CountByProfessionalIdAsync(int professionalId)
            => await _repository.CountByProfessionalIdAsync(professionalId);

        public async Task<int> CountByOfficeIdAsync(int officeId)
            => await _repository.CountByOfficeIdAsync(officeId);

            public async Task<int> CountTodayAsync(DateTime utcToday)
                => await _repository.CountTodayAsync(utcToday);

            public async Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus)
                => await _repository.CountByPaymentStatusAsync(paymentStatus);

            public async Task<int> CountByStatusAsync(SessionStatus status)
                => await _repository.CountByStatusAsync(status);

            public async Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay)
                => await _repository.CountByStatusOnDateAsync(status, utcDay);

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
                windowInMinutes: _professionalConflictWindowMinutes,
                excludeSessionId: excludeSessionId);

            if (hasConflict)
            {
                throw new BusinessValidationException(
                    $"El profesional ya tiene una sesion asignada en un rango de +/- {_professionalConflictWindowMinutes} minutos para el horario seleccionado.",
                    nameof(Session.FechaHora));
            }
        }
    }
}
