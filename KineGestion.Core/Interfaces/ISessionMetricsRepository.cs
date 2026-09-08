using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Conteos y segmentos de KPIs con alcance por rol del usuario actual
    /// (Admin o sin usuario ve todo; Kinesiologo/Asistente ve solo su profesional).
    /// Usada por el dashboard y análisis de cancelaciones.
    /// </summary>
    public interface ISessionMetricsRepository
    {
        Task<int> CountAsync();
        Task<int> CountTodayAsync(DateTime utcToday);
        Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus);
        Task<int> CountByStatusAsync(SessionStatus status);
        Task<int> CountByCancellationReasonAsync(CancellationReason reason);
        Task<IDictionary<CancellationReason, int>> CountByCancellationReasonInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountLateCancellationsInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusAndPaymentStatusAsync(SessionStatus status, PaymentStatus paymentStatus);
        Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay);
        Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByStatusAndPaymentStatusInRangeAsync(SessionStatus status, PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByProfessionalAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByTimeSlotAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
    }
}