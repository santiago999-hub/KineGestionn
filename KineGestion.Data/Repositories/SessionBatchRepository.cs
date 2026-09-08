using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;

namespace KineGestion.Data.Repositories
{
    /// <summary>
    /// Actualizaciones masivas por set (ExecuteUpdate) con nota de auditoría en InternalNotes.
    /// Set-based: no materializa las sesiones en memoria ni toca el ChangeTracker.
    /// </summary>
    public class SessionBatchRepository : SessionRepositoryBase, ISessionBatchRepository
    {
        public SessionBatchRepository(AppDbContext context) : base(context)
        {
        }

        public SessionBatchRepository(AppDbContext context, ICurrentUserProvider currentUserProvider) : base(context, currentUserProvider)
        {
        }

        public async Task<(int UpdatedCount, int SkippedCount)> MarkCompletedPendingAsPaidBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc)
        {
            var normalizedIds = sessionIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
                return (0, 0);

            var note = $"[{actionAtUtc:yyyy-MM-dd HH:mm 'UTC'}] COBRO_REGISTRADO";
            var candidates = _context.Sessions
                .Where(s => normalizedIds.Contains(s.Id)
                    && s.Status == SessionStatus.Completed
                    && s.PaymentStatus == PaymentStatus.Pending);

            var updatedCount = await candidates.ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(s => s.InternalNotes, s => string.IsNullOrWhiteSpace(s.InternalNotes)
                    ? note
                    : s.InternalNotes + Environment.NewLine + note));

            return (updatedCount, normalizedIds.Count - updatedCount);
        }

        public async Task<(int UpdatedCount, int SkippedCount)> MarkPaidAsPendingBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc)
        {
            var normalizedIds = sessionIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
                return (0, 0);

            var note = $"[{actionAtUtc:yyyy-MM-dd HH:mm 'UTC'}] COBRO_REABIERTO";
            var candidates = _context.Sessions
                .Where(s => normalizedIds.Contains(s.Id)
                    && s.PaymentStatus == PaymentStatus.Paid);

            var updatedCount = await candidates.ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.PaymentStatus, PaymentStatus.Pending)
                .SetProperty(s => s.InternalNotes, s => string.IsNullOrWhiteSpace(s.InternalNotes)
                    ? note
                    : s.InternalNotes + Environment.NewLine + note));

            return (updatedCount, normalizedIds.Count - updatedCount);
        }
    }
}