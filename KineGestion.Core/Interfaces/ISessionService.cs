using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionService
    {
        Task<Session?> GetByIdAsync(int id);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo, string? sortBy, string? sortDir);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo);
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<int> CountAsync();
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<int> CountTodayAsync(DateTime utcToday);
        Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus);
        Task<int> CountByStatusAsync(SessionStatus status);
        Task<int> CountByStatusAndPaymentStatusAsync(SessionStatus status, PaymentStatus paymentStatus);
        Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay);
        Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusAndPaymentStatusInRangeAsync(SessionStatus status, PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByProfessionalAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByTimeSlotAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IEnumerable<BillingFollowUpCandidateDto>> GetBillingFollowUpCandidatesAsync(DateTime asOfUtc, int minAgeDays, int maxAgeDays);
        Task<ReminderFunnelDto> BuildReminderFunnelAsync(IReadOnlyCollection<int> sentSessionIds, DateTime fromSentUtc, DateTime toSentUtc);
        Task ConfirmByReminderAsync(int sessionId);
        Task CancelByReminderAsync(int sessionId);
        Task CancelAsync(int sessionId, CancellationReason reason, string? observation);
        Task<Session> ReprogramAsync(int sourceSessionId, DateTime newFechaHora);
        Task<IReadOnlyList<AvailableSlotDto>> SuggestAvailableSlotsAsync(int professionalId, DateTime fromUtc, int dayCount = 5, int count = 3);
        Task<int> CountByCancellationReasonAsync(CancellationReason reason);
        Task<IDictionary<CancellationReason, int>> CountByCancellationReasonInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        CancellationTiming GetCancellationTiming(Session session);
        Task<int> CountLateCancellationsInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task SetPaymentStatusAsync(int sessionId, PaymentStatus paymentStatus);
        Task<(int UpdatedCount, int SkippedCount)> MarkCompletedPendingAsPaidBatchAsync(IReadOnlyCollection<int> sessionIds);
        Task<(int UpdatedCount, int SkippedCount)> MarkPaidAsPendingBatchAsync(IReadOnlyCollection<int> sessionIds);
        Task<Session> CreateAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
