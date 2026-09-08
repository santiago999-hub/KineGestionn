using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Proyecciones de lectura para pantallas y pipeline de recordatorios/cobranza.
    /// Los listados paginados filtran por rol; los candidatos del pipeline son
    /// consultas de background que no requieren contexto de usuario.
    /// </summary>
    public interface ISessionQueryRepository
    {
        /// <summary>Proyección optimizada para la tabla admin: sin cargar nav properties completas.</summary>
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo, string? sortBy, string? sortDir);
        /// <summary>Proyección optimizada para la agenda del kinesiológo: sin nav properties.</summary>
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo);
        Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<IEnumerable<BillingFollowUpCandidateDto>> GetBillingFollowUpCandidatesAsync(DateTime asOfUtc, int minAgeDays, int maxAgeDays);
        Task<IReadOnlyList<SessionFunnelOutcomeDto>> GetSessionFunnelOutcomesAsync(IReadOnlyCollection<int> sessionIds, DateTime fromSentUtc, DateTime toSentUtc);
    }
}