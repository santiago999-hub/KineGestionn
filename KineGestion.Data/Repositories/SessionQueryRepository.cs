using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;

namespace KineGestion.Data.Repositories
{
    /// <summary>
    /// Proyecciones de lectura: listados paginados para pantallas (con filtro por rol)
    /// y candidatos del pipeline de recordatorios/cobranza (consultas de background).
    /// Proyección SQL directa: evita cargar nav properties completas.
    /// </summary>
    public class SessionQueryRepository : SessionRepositoryBase, ISessionQueryRepository
    {
        public SessionQueryRepository(AppDbContext context) : base(context)
        {
        }

        public SessionQueryRepository(AppDbContext context, ICurrentUserProvider currentUserProvider) : base(context, currentUserProvider)
        {
        }

        /// <summary>
        /// Aplica el pipeline de filtrado compartido por los listados paginados:
        /// término de búsqueda, estado, estado de pago y rango de fechas.
        /// La búsqueda incluye los campos del professional solo cuando la vista lo requiere
        /// (listado admin); en el listado por profesional el dueño ya es el usuario actual.
        /// </summary>
        private static IQueryable<Session> ApplyCommonFilters(
            IQueryable<Session> query,
            string? search,
            bool includeProfessionalSearch,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    (s.Patient != null && (
                        s.Patient.Nombre.Contains(term) ||
                        s.Patient.Apellido.Contains(term) ||
                        s.Patient.DNI.Contains(term))) ||
                    (includeProfessionalSearch && s.Professional != null && (
                        s.Professional.Nombre.Contains(term) ||
                        s.Professional.Apellido.Contains(term) ||
                        s.Professional.Matricula.Contains(term))) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)        query = query.Where(s => s.Status == status.Value);
            if (paymentStatus.HasValue) query = query.Where(s => s.PaymentStatus == paymentStatus.Value);
            if (dateFrom.HasValue)      query = query.Where(s => s.FechaHora >= dateFrom.Value.Date);
            if (dateTo.HasValue)        query = query.Where(s => s.FechaHora < dateTo.Value.Date.AddDays(1));

            return query;
        }

        /// <summary>
        /// Proyección SQL directa: solo trae los campos necesarios para la tabla admin.
        /// Evita cargar nav properties completas de Patient, Professional, Treatment y Office.
        /// Filtrado por rol: Admin ve todo, Kinesiologo ve sus sesiones, Asistente ve sin notas clínicas.
        /// </summary>
        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(
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
            var baseQuery = _context.Sessions.AsNoTracking().AsQueryable();

            // IDOR protection: non-admin users only see their own professional's sessions
            var profFilter = GetProfessionalIdFilter();
            if (profFilter.HasValue)
                baseQuery = baseQuery.Where(s => s.ProfessionalId == profFilter.Value);

            baseQuery = ApplyCommonFilters(
                baseQuery,
                search,
                includeProfessionalSearch: true,
                status,
                paymentStatus,
                dateFrom,
                dateTo);

            int totalCount = await baseQuery.CountAsync();

            var sortField = string.IsNullOrWhiteSpace(sortBy) ? "fecha" : sortBy.Trim().ToLowerInvariant();
            var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

            var sortedQuery = (sortField, descending) switch
            {
                ("estado", true)  => baseQuery.OrderByDescending(s => s.Status).ThenByDescending(s => s.FechaHora),
                ("estado", false) => baseQuery.OrderBy(s => s.Status).ThenByDescending(s => s.FechaHora),
                (_, true)  => baseQuery.OrderByDescending(s => s.FechaHora),
                _ => baseQuery.OrderBy(s => s.FechaHora)
            };

            var items = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SessionListDto(
                    s.Id,
                    s.FechaHora,
                    s.Status,
                    s.PaymentStatus,
                    s.NroSesionEnTratamiento,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : string.Empty,
                    s.Professional != null ? s.Professional.Apellido + ", " + s.Professional.Nombre : string.Empty,
                    s.Treatment != null ? s.Treatment.Descripcion : null,
                    s.Office != null ? s.Office.Name : null,
                    s.EvolutionLockedAt.HasValue,
                    s.CancellationReason,
                    s.CancellationObs
                ))
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Proyección SQL directa para la agenda del kinesiológo.
        /// No carga nav properties de Professional (ya está filtrado por professionalId).
        /// </summary>
        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(
            int professionalId,
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var baseQuery = _context.Sessions
                .AsNoTracking()
                .Where(s => s.ProfessionalId == professionalId)
                .AsQueryable();

            baseQuery = ApplyCommonFilters(
                baseQuery,
                search,
                includeProfessionalSearch: false,
                status,
                paymentStatus,
                dateFrom,
                dateTo);

            int totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderByDescending(s => s.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SessionListDto(
                    s.Id,
                    s.FechaHora,
                    s.Status,
                    s.PaymentStatus,
                    s.NroSesionEnTratamiento,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : string.Empty,
                    string.Empty,   // el profesional ya es el usuario actual
                    s.Treatment != null ? s.Treatment.Descripcion : null,
                    s.Office != null ? s.Office.Name : null,
                    s.EvolutionLockedAt.HasValue,
                    s.CancellationReason,
                    s.CancellationObs
                ))
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                .AsNoTracking()
                .Where(s => s.Status == SessionStatus.Pending
                    && s.FechaHora >= fromInclusiveUtc
                    && s.FechaHora < toExclusiveUtc)
                .OrderBy(s => s.FechaHora)
                .Select(s => new SessionReminderCandidateDto(
                    s.Id,
                    s.FechaHora,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : "Paciente",
                    s.Patient != null ? s.Patient.Email : null,
                    s.Patient != null ? s.Patient.Telefono : null,
                    s.Professional != null ? s.Professional.Apellido + ", " + s.Professional.Nombre : "Profesional",
                    s.Treatment != null ? s.Treatment.Descripcion : null
                ))
                .ToListAsync();

        public async Task<IEnumerable<BillingFollowUpCandidateDto>> GetBillingFollowUpCandidatesAsync(DateTime asOfUtc, int minAgeDays, int maxAgeDays)
        {
            var cutoffOldest = asOfUtc.Date.AddDays(-maxAgeDays);
            var cutoffMostRecent = asOfUtc.Date.AddDays(-minAgeDays).AddDays(1);

            var rows = await _context.Sessions
                .AsNoTracking()
                .Where(s => s.Status == SessionStatus.Completed
                    && s.PaymentStatus == PaymentStatus.Pending
                    && s.FechaHora >= cutoffOldest
                    && s.FechaHora < cutoffMostRecent)
                .Select(s => new
                {
                    s.Id,
                    s.FechaHora,
                    s.Patient,
                    s.Professional,
                    s.Treatment
                })
                .ToListAsync();

            return rows.Select(s => new BillingFollowUpCandidateDto(
                s.Id,
                s.FechaHora,
                s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : "Paciente",
                s.Patient != null ? s.Patient.Email : null,
                s.Patient != null ? s.Patient.Telefono : null,
                s.Professional != null ? s.Professional.Apellido + ", " + s.Professional.Nombre : "Profesional",
                s.Treatment != null ? s.Treatment.Descripcion : null,
                Math.Max(0, (asOfUtc.Date - s.FechaHora.Date).Days)
            ));
        }

        /// <summary>
        /// Embudo de recordatorios: para las sesiones que recibieron al menos un envío,
        /// devuelve su estado y si tienen nota de confirmación/cancelación del paciente.
        /// InternalNotes se lee descifrada por EF; la clasificación se hace en memoria
        /// sobre el volumen acotado de sesiones enviadas (evita Memory Bomb).
        /// </summary>
        public async Task<IReadOnlyList<SessionFunnelOutcomeDto>> GetSessionFunnelOutcomesAsync(
            IReadOnlyCollection<int> sessionIds,
            DateTime fromSentUtc,
            DateTime toSentUtc)
        {
            var ids = sessionIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return Array.Empty<SessionFunnelOutcomeDto>();

            var sessions = await _context.Sessions
                .AsNoTracking()
                .Where(s => ids.Contains(s.Id)
                    && s.FechaHora >= fromSentUtc
                    && s.FechaHora < toSentUtc)
                .Select(s => new
                {
                    s.Id,
                    s.Status,
                    s.CancelledAt,
                    s.InternalNotes
                })
                .ToListAsync();

            return sessions.Select(s => new SessionFunnelOutcomeDto(
                s.Id,
                s.Status,
                s.InternalNotes != null && s.InternalNotes.Contains("CONFIRMADA_PACIENTE", StringComparison.OrdinalIgnoreCase),
                s.InternalNotes != null && s.InternalNotes.Contains("CANCELADA_PACIENTE", StringComparison.OrdinalIgnoreCase),
                s.CancelledAt))
                .ToList();
        }
    }
}