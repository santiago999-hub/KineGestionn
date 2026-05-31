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
            => await QueryCache.GetOrCreateAsync(
                $"sessions:admin:paged:{page}:{pageSize}:{NormalizeSearch(search)}:{status?.ToString() ?? "_"}:{paymentStatus?.ToString() ?? "_"}:{NormalizeDate(dateFrom)}:{NormalizeDate(dateTo)}:{NormalizeSort(sortBy)}:{NormalizeSort(sortDir)}",
                () => _repository.GetPagedListForAdminAsync(page, pageSize, search, status, paymentStatus, dateFrom, dateTo, sortBy, sortDir),
                TimeSpan.FromSeconds(8));

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
            => await QueryCache.GetOrCreateAsync(
                $"sessions:professional:{professionalId}:paged:{page}:{pageSize}:{NormalizeSearch(search)}:{status?.ToString() ?? "_"}:{paymentStatus?.ToString() ?? "_"}:{NormalizeDate(dateFrom)}:{NormalizeDate(dateTo)}",
                () => _repository.GetPagedListByProfessionalAsync(professionalId, page, pageSize, search, status, paymentStatus, dateFrom, dateTo),
                TimeSpan.FromSeconds(8));

        /// <summary>OBSOLETO: carga todas las sesiones sin filtro. Ver interfaz para detalles.</summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync o GetPagedListByProfessionalAsync.")]
        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId)
            => await _repository.GetByProfessionalIdAsync(professionalId);

        public async Task<int> CountAsync()
            => await QueryCache.GetOrCreateAsync(
                "sessions:count:all",
                () => _repository.CountAsync(),
                TimeSpan.FromSeconds(10));

        public async Task<int> CountByTreatmentIdAsync(int treatmentId)
            => await _repository.CountByTreatmentIdAsync(treatmentId);

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await _repository.CountByPatientIdAsync(patientId);

        public async Task<int> CountByProfessionalIdAsync(int professionalId)
            => await _repository.CountByProfessionalIdAsync(professionalId);

        public async Task<int> CountByOfficeIdAsync(int officeId)
            => await _repository.CountByOfficeIdAsync(officeId);

            public async Task<int> CountTodayAsync(DateTime utcToday)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:today:{utcToday:yyyyMMdd}",
                    () => _repository.CountTodayAsync(utcToday),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:payment:{paymentStatus}",
                    () => _repository.CountByPaymentStatusAsync(paymentStatus),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByStatusAsync(SessionStatus status)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:status:{status}",
                    () => _repository.CountByStatusAsync(status),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByStatusAndPaymentStatusAsync(SessionStatus status, PaymentStatus paymentStatus)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:status:{status}:payment:{paymentStatus}",
                    () => _repository.CountByStatusAndPaymentStatusAsync(status, paymentStatus),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:status:{status}:day:{utcDay:yyyyMMdd}",
                    () => _repository.CountByStatusOnDateAsync(status, utcDay),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:range:{fromInclusiveUtc:yyyyMMddHHmmss}:{toExclusiveUtc:yyyyMMddHHmmss}",
                    () => _repository.CountInRangeAsync(fromInclusiveUtc, toExclusiveUtc),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:status:{status}:range:{fromInclusiveUtc:yyyyMMddHHmmss}:{toExclusiveUtc:yyyyMMddHHmmss}",
                    () => _repository.CountByStatusInRangeAsync(status, fromInclusiveUtc, toExclusiveUtc),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:payment:{paymentStatus}:range:{fromInclusiveUtc:yyyyMMddHHmmss}:{toExclusiveUtc:yyyyMMddHHmmss}",
                    () => _repository.CountByPaymentStatusInRangeAsync(paymentStatus, fromInclusiveUtc, toExclusiveUtc),
                    TimeSpan.FromSeconds(10));

            public async Task<int> CountByStatusAndPaymentStatusInRangeAsync(SessionStatus status, PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:status:{status}:payment:{paymentStatus}:range:{fromInclusiveUtc:yyyyMMddHHmmss}:{toExclusiveUtc:yyyyMMddHHmmss}",
                    () => _repository.CountByStatusAndPaymentStatusInRangeAsync(status, paymentStatus, fromInclusiveUtc, toExclusiveUtc),
                    TimeSpan.FromSeconds(10));

            public async Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await _repository.GetReminderCandidatesAsync(fromInclusiveUtc, toExclusiveUtc);

            public async Task ConfirmByReminderAsync(int sessionId)
            {
                var session = await _repository.GetByIdAsync(sessionId);
                if (session is null)
                    throw new BusinessValidationException("La sesión no existe.", nameof(Session.Id));

                if (session.Status == SessionStatus.Canceled)
                    throw new BusinessValidationException("La sesión ya está cancelada.", nameof(Session.Status));

                AppendSystemNote(session, "CONFIRMADA_PACIENTE");
                await _repository.UpdateAsync(session);
                QueryCache.InvalidatePrefix("sessions:");
            }

            public async Task CancelByReminderAsync(int sessionId)
            {
                var session = await _repository.GetByIdAsync(sessionId);
                if (session is null)
                    throw new BusinessValidationException("La sesión no existe.", nameof(Session.Id));

                if (session.Status == SessionStatus.Canceled)
                    return;

                session.Status = SessionStatus.Canceled;
                AppendSystemNote(session, "CANCELADA_PACIENTE");
                await _repository.UpdateAsync(session);
                QueryCache.InvalidatePrefix("sessions:");
            }

            public async Task SetPaymentStatusAsync(int sessionId, PaymentStatus paymentStatus)
            {
                var session = await _repository.GetByIdAsync(sessionId);
                if (session is null)
                    throw new BusinessValidationException("La sesión no existe.", nameof(Session.Id));

                if (session.PaymentStatus == paymentStatus)
                    return;

                session.PaymentStatus = paymentStatus;
                AppendSystemNote(session, paymentStatus == PaymentStatus.Paid ? "COBRO_REGISTRADO" : "COBRO_REABIERTO");
                await _repository.UpdateAsync(session);
                QueryCache.InvalidatePrefix("sessions:");
            }

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
            var created = await _repository.AddAsync(session);
            QueryCache.InvalidatePrefix("sessions:");
            return created;
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

            var updated = await _repository.UpdateAsync(session);
            QueryCache.InvalidatePrefix("sessions:");
            return updated;
        }

        public async Task DeleteAsync(int id)
        {
            await _repository.DeleteAsync(id);
            QueryCache.InvalidatePrefix("sessions:");
        }

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

        private static void AppendSystemNote(Session session, string action)
        {
            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
            var note = $"[{stamp}] {action}";
            session.InternalNotes = string.IsNullOrWhiteSpace(session.InternalNotes)
                ? note
                : session.InternalNotes + Environment.NewLine + note;
        }

        private static string NormalizeSearch(string? search)
            => string.IsNullOrWhiteSpace(search) ? "_" : search.Trim().ToLowerInvariant();

        private static string NormalizeSort(string? value)
            => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().ToLowerInvariant();

        private static string NormalizeDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("yyyyMMddHHmmss") : "_";
    }
}
