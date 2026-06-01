using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionRepository
    {
        Task<Session?> GetByIdAsync(int id);
        /// <summary>
        /// OBSOLETO: Carga TODAS las sesiones en memoria sin paginado. Usar GetPagedListForAdminAsync.
        /// Riesgo de OOM (Out Of Memory) en producción con volumen real de datos.
        /// </summary>
        [Obsolete("Peligro de Memory Bomb en producción. Usar GetPagedListForAdminAsync con pageSize paramétrico.")]
        Task<IEnumerable<Session>> GetAllAsync();
        /// <summary>
        /// OBSOLETO: Carga nav properties completas (Patient, Professional, Treatment, Office) en memoria.
        /// Usar GetPagedListForAdminAsync: proyección SQL que trae solo los campos necesarios para la tabla.
        /// </summary>
        [Obsolete("Carga entidades completas con 4 JOINs. Usar GetPagedListForAdminAsync.")]
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo, string? sortBy, string? sortDir);
        /// <summary>Proyección optimizada para la tabla admin: sin cargar nav properties completas.</summary>
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo, string? sortBy, string? sortDir);
        /// <summary>Carga entidades completas con 3 JOINs. Usar <see cref="GetPagedListByProfessionalAsync"/>.</summary>
        [Obsolete("Carga entidades completas con 3 JOINs. Usar GetPagedListByProfessionalAsync.")]
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus);
        /// <summary>Proyección optimizada para la agenda del kinesiológo: sin nav properties.</summary>
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo);
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<IEnumerable<Session>> GetByTreatmentIdAsync(int treatmentId);
        Task<bool> ExistsProfessionalConflictAsync(int professionalId, DateTime fechaHora, int windowInMinutes = 45, int? excludeSessionId = null);
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<int> CountAsync();
        Task<int> CountTodayAsync(DateTime utcToday);
        Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus);
        Task<int> CountByStatusAsync(SessionStatus status);
        Task<int> CountByStatusAndPaymentStatusAsync(SessionStatus status, PaymentStatus paymentStatus);
        Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay);
        Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusAndPaymentStatusInRangeAsync(SessionStatus status, PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<(int UpdatedCount, int SkippedCount)> MarkCompletedPendingAsPaidBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc);
        Task<(int UpdatedCount, int SkippedCount)> MarkPaidAsPendingBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc);
        Task<Session> AddAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
