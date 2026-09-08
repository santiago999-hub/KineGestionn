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
    /// Conteos y segmentos de KPIs con alcance por rol del usuario actual (ApplyCountScope).
    /// Proyecciones en SQL: los conteos y agrupaciones se resuelven en el servidor.
    /// </summary>
    public class SessionMetricsRepository : SessionRepositoryBase, ISessionMetricsRepository
    {
        public SessionMetricsRepository(AppDbContext context) : base(context)
        {
        }

        public SessionMetricsRepository(AppDbContext context, ICurrentUserProvider currentUserProvider) : base(context, currentUserProvider)
        {
        }

        public async Task<int> CountAsync()
            => await ApplyCountScope(_context.Sessions.AsNoTracking()).CountAsync();

        public async Task<int> CountTodayAsync(DateTime utcToday)
        {
            var tomorrow = utcToday.Date.AddDays(1);
            return await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.FechaHora >= utcToday.Date && s.FechaHora < tomorrow);
        }

        public async Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.PaymentStatus == paymentStatus);

        public async Task<int> CountByStatusAsync(SessionStatus status)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.Status == status);

        public async Task<int> CountByCancellationReasonAsync(CancellationReason reason)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.CancellationReason == reason);

        public async Task<IDictionary<CancellationReason, int>> CountByCancellationReasonInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
        {
            var query = ApplyCountScope(_context.Sessions.AsNoTracking())
                .Where(s => s.CancellationReason.HasValue
                    && s.FechaHora >= fromInclusiveUtc
                    && s.FechaHora < toExclusiveUtc);

            var groups = await query
                .GroupBy(s => s.CancellationReason!.Value)
                .Select(g => new { Reason = g.Key, Count = g.Count() })
                .ToListAsync();

            return groups.ToDictionary(g => g.Reason, g => g.Count);
        }

        public async Task<int> CountLateCancellationsInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
        {
            var query = ApplyCountScope(_context.Sessions.AsNoTracking())
                .Where(s => s.Status == SessionStatus.Canceled
                    && s.CancelledAt.HasValue
                    && s.FechaHora >= fromInclusiveUtc
                    && s.FechaHora < toExclusiveUtc);

            // Clasificación tardía = menos de 24h de antelación (FechaHora - CancelledAt < 24h).
            // Forma traducible por EF: CancelledAt > FechaHora - 24h. Se resuelve en SQL sin materializar.
            return await query
                .CountAsync(s => s.CancelledAt!.Value > s.FechaHora.AddHours(-24));
        }

        public async Task<int> CountByStatusAndPaymentStatusAsync(SessionStatus status, PaymentStatus paymentStatus)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.Status == status && s.PaymentStatus == paymentStatus);

        public async Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay)
        {
            var tomorrow = utcDay.Date.AddDays(1);
            return await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.Status == status && s.FechaHora >= utcDay.Date && s.FechaHora < tomorrow);
        }

        public async Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.Status == status && s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.PaymentStatus == paymentStatus && s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<int> CountByStatusAndPaymentStatusInRangeAsync(SessionStatus status, PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await ApplyCountScope(_context.Sessions.AsNoTracking())
                .CountAsync(s => s.Status == status
                    && s.PaymentStatus == paymentStatus
                    && s.FechaHora >= fromInclusiveUtc
                    && s.FechaHora < toExclusiveUtc);

        public async Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByProfessionalAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
        {
            var query = ApplyCountScope(_context.Sessions.AsNoTracking())
                .Where(s => s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

            var groups = await query
                .GroupBy(s => s.Professional)
                .Select(g => new KpiSegmentDto(
                    g.Key != null ? g.Key.Id.ToString() : "?",
                    g.Key != null ? (g.Key.Apellido + ", " + g.Key.Nombre) : "Sin profesional",
                    g.Count(),
                    g.Count(s => s.Status == SessionStatus.Pending),
                    g.Count(s => s.Status == SessionStatus.Completed),
                    g.Count(s => s.Status == SessionStatus.Canceled),
                    g.Count(s => s.Status == SessionStatus.Completed && s.PaymentStatus == PaymentStatus.Pending),
                    g.Count(s => s.Status == SessionStatus.Completed && s.PaymentStatus == PaymentStatus.Paid)))
                .ToListAsync();

            return groups
                .OrderByDescending(g => g.Canceled + g.CompletedPending)
                .ToList();
        }

        public async Task<IReadOnlyList<KpiSegmentDto>> GetKpiSegmentsByTimeSlotAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
        {
            var query = ApplyCountScope(_context.Sessions.AsNoTracking())
                .Where(s => s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

            var groups = await query
                .GroupBy(s => s.FechaHora.Hour)
                .Select(g => new KpiSegmentDto(
                    g.Key.ToString("00"),
                    g.Key.ToString("00") + ":00",
                    g.Count(),
                    g.Count(s => s.Status == SessionStatus.Pending),
                    g.Count(s => s.Status == SessionStatus.Completed),
                    g.Count(s => s.Status == SessionStatus.Canceled),
                    g.Count(s => s.Status == SessionStatus.Completed && s.PaymentStatus == PaymentStatus.Pending),
                    g.Count(s => s.Status == SessionStatus.Completed && s.PaymentStatus == PaymentStatus.Paid)))
                .ToListAsync();

            return groups
                .OrderBy(g => g.SegmentKey)
                .ToList();
        }
    }
}