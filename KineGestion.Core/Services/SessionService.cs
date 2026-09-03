using System;
using System.Collections.Generic;
using System.Linq;
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

            public async Task CancelAsync(int sessionId, CancellationReason reason, string? observation)
            {
                var session = await _repository.GetByIdAsync(sessionId);
                if (session is null)
                    throw new BusinessValidationException("La sesión no existe.", nameof(Session.Id));

                if (session.Status == SessionStatus.Canceled)
                    throw new BusinessValidationException("La sesión ya está cancelada.", nameof(Session.Status));

                if (session.Status == SessionStatus.Completed)
                    throw new BusinessValidationException("No se puede cancelar una sesión completada.", nameof(Session.Status));

                session.Status = SessionStatus.Canceled;
                session.CancellationReason = reason;
                session.CancellationObs = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
                session.CancelledAt = DateTime.UtcNow;
                AppendSystemNote(session, $"CANCELADA_MOTIVO_{reason}");
                await _repository.UpdateAsync(session);
                QueryCache.InvalidatePrefix("sessions:");
            }

            /// <summary>
            /// Recaptura: crea una nueva sesión a partir de una cancelada, con la misma
            /// ficha clínica y tratamiento pero un nuevo horario.
            /// </summary>
            public async Task<Session> ReprogramAsync(int sourceSessionId, DateTime newFechaHora)
            {
                var source = await _repository.GetByIdAsync(sourceSessionId);
                if (source is null)
                    throw new BusinessValidationException("La sesión original no existe.", nameof(Session.Id));
                if (source.Status != SessionStatus.Canceled)
                    throw new BusinessValidationException("Solo se puede reprogramar una sesión cancelada.", nameof(Session.Status));

                await ValidateProfessionalAvailabilityAsync(source.ProfessionalId, newFechaHora);

                int sesionesEnTratamiento = await _repository.CountByTreatmentIdAsync(source.TreatmentId);
                var treatment = await _treatmentRepository.GetByIdAsync(source.TreatmentId);

                var nueva = new Session
                {
                    FechaHora = newFechaHora,
                    PatientId = source.PatientId,
                    ProfessionalId = source.ProfessionalId,
                    TreatmentId = source.TreatmentId,
                    OfficeId = source.OfficeId,
                    Observaciones = source.Observaciones,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    NroSesionEnTratamiento = sesionesEnTratamiento + 1
                };

                if (treatment is not null && sesionesEnTratamiento >= treatment.CantidadSesionesTotales)
                    throw new BusinessValidationException(
                        $"El tratamiento ya alcanzó el límite de {treatment.CantidadSesionesTotales} sesiones.",
                        nameof(Session.TreatmentId));

                var created = await _repository.AddAsync(nueva);
                QueryCache.InvalidatePrefix("sessions:");
                return created;
            }

            /// <summary>
            /// Sugiere turnos alternativos disponibles para un profesional, omitiendo horarios
            /// ocupados (con la ventana de conflicto) y horarios pasados.
            /// </summary>
            public async Task<IReadOnlyList<AvailableSlotDto>> SuggestAvailableSlotsAsync(
                int professionalId,
                DateTime fromUtc,
                int dayCount = 5,
                int count = 3)
            {
                var normalizedFrom = fromUtc.Date;
                var toExclusive = normalizedFrom.AddDays(Math.Max(1, Math.Min(dayCount, 30)));

                var busyTimes = await _repository.GetProfessionalBusyTimesAsync(professionalId, normalizedFrom, toExclusive);
                var isBusy = new HashSet<DateTime>(busyTimes);
                var nowUtc = DateTime.UtcNow;

                var slots = new List<AvailableSlotDto>();
                for (var day = normalizedFrom;
                     day < toExclusive && slots.Count < count;
                     day = day.AddDays(1))
                {
                    if (isWeekend(day)) continue;

                    for (var slot = day.AddHours(9); slot.Hour < 17 && slots.Count < count; slot = slot.AddHours(1))
                    {
                        if (slot <= nowUtc) continue;
                        if (IsSlotOccupied(slot, isBusy, _professionalConflictWindowMinutes)) continue;

                        slots.Add(new AvailableSlotDto(slot, $"{slot:dd/MM/yyyy} {slot:HH:mm} hs"));
                        if (slots.Count >= count) break;
                    }
                }

                return slots;
            }

            private static bool IsSlotOccupied(DateTime slot, HashSet<DateTime> busyTimes, int windowMinutes)
            {
                foreach (var busy in busyTimes)
                {
                    if (Math.Abs((busy - slot).TotalMinutes) <= windowMinutes)
                        return true;
                }
                return false;
            }

            private static bool isWeekend(DateTime d)
                => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;


            public async Task<int> CountByCancellationReasonAsync(CancellationReason reason)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:cancelreason:{reason}",
                    () => _repository.CountByCancellationReasonAsync(reason),
                    TimeSpan.FromSeconds(10));

            public async Task<IDictionary<CancellationReason, int>> CountByCancellationReasonInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
                => await QueryCache.GetOrCreateAsync(
                    $"sessions:count:cancelreason:range:{fromInclusiveUtc:yyyyMMddHHmmss}:{toExclusiveUtc:yyyyMMddHHmmss}",
                    () => _repository.CountByCancellationReasonInRangeAsync(fromInclusiveUtc, toExclusiveUtc),
                    TimeSpan.FromSeconds(10));

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

            public async Task<(int UpdatedCount, int SkippedCount)> MarkCompletedPendingAsPaidBatchAsync(IReadOnlyCollection<int> sessionIds)
            {
                var normalizedIds = sessionIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (normalizedIds.Count == 0)
                    return (0, 0);

                var result = await _repository.MarkCompletedPendingAsPaidBatchAsync(normalizedIds, DateTime.UtcNow);
                if (result.UpdatedCount > 0)
                    QueryCache.InvalidatePrefix("sessions:");

                return result;
            }

            public async Task<(int UpdatedCount, int SkippedCount)> MarkPaidAsPendingBatchAsync(IReadOnlyCollection<int> sessionIds)
            {
                var normalizedIds = sessionIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (normalizedIds.Count == 0)
                    return (0, 0);

                var result = await _repository.MarkPaidAsPendingBatchAsync(normalizedIds, DateTime.UtcNow);
                if (result.UpdatedCount > 0)
                    QueryCache.InvalidatePrefix("sessions:");

                return result;
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
