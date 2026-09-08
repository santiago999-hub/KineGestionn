using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Data.Repositories
{
    public sealed class DispatchJobRepository : IDispatchJobRepository
    {
        private readonly AppDbContext _context;

        public DispatchJobRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<DispatchJob?> FindOpenAsync(int sessionId, string dispatchType, string payloadHash, CancellationToken cancellationToken)
            => _context.DispatchJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    j => j.SessionId == sessionId
                        && j.DispatchType == dispatchType
                        && j.PayloadHash == payloadHash
                        && (j.Status == DispatchJobStatus.Pending || j.Status == DispatchJobStatus.Processing),
                    cancellationToken);

        public async Task AddAsync(DispatchJob job, CancellationToken cancellationToken)
        {
            _context.DispatchJobs.Add(job);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DispatchJob>> ClaimNextBatchAsync(
            int batchSize,
            TimeSpan leaseDuration,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var claimToken = Guid.NewGuid().ToString("N");
            var leaseCutoff = nowUtc.Subtract(leaseDuration);

            // Reclamo atómico fila por fila: el subquery elige la fila elegible más antigua.
            // Al estar marcada con nuestro ClaimToken, ninguna corrida concurrente la re-clama.
            for (var claimed = 0; claimed < batchSize; claimed++)
            {
                var affected = await _context.DispatchJobs
                    .Where(j =>
                        (j.Status == DispatchJobStatus.Pending
                         || (j.Status == DispatchJobStatus.Processing && j.ClaimedAtUtc != null && j.ClaimedAtUtc < leaseCutoff))
                        && (j.NextAttemptAtUtc == null || j.NextAttemptAtUtc <= nowUtc)
                        && j.Id == _context.DispatchJobs
                            .Where(j2 =>
                                (j2.Status == DispatchJobStatus.Pending
                                 || (j2.Status == DispatchJobStatus.Processing && j2.ClaimedAtUtc != null && j2.ClaimedAtUtc < leaseCutoff))
                                && (j2.NextAttemptAtUtc == null || j2.NextAttemptAtUtc <= nowUtc))
                            .OrderBy(j2 => j2.CreatedAtUtc)
                            .Select(j2 => (int?)j2.Id)
                            .FirstOrDefault())
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(j => j.Status, DispatchJobStatus.Processing)
                            .SetProperty(j => j.ClaimToken, claimToken)
                            .SetProperty(j => j.ClaimedAtUtc, nowUtc)
                            .SetProperty(j => j.NextAttemptAtUtc, (DateTime?)null),
                        cancellationToken);

                if (affected == 0)
                    break;
            }

            return await _context.DispatchJobs
                .AsNoTracking()
                .Where(j => j.ClaimToken == claimToken && j.Status == DispatchJobStatus.Processing)
                .OrderBy(j => j.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task MarkSucceededAsync(int id, string? resultJson, DateTime nowUtc, CancellationToken cancellationToken)
        {
            await _context.DispatchJobs
                .Where(j => j.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(j => j.Status, DispatchJobStatus.Succeeded)
                        .SetProperty(j => j.ProcessedAtUtc, nowUtc)
                        .SetProperty(j => j.ResultJson, resultJson)
                        .SetProperty(j => j.LastError, (string?)null),
                    cancellationToken);
        }

        public async Task MarkFailedAsync(int id, string error, TimeSpan retryDelay, int maxAttempts, DateTime nowUtc, CancellationToken cancellationToken)
        {
            var terminal = (await _context.DispatchJobs
                .Where(j => j.Id == id)
                .Select(j => j.Attempts)
                .FirstOrDefaultAsync(cancellationToken)) + 1 >= maxAttempts;

            await _context.DispatchJobs
                .Where(j => j.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(j => j.Status, terminal ? DispatchJobStatus.Failed : DispatchJobStatus.Pending)
                        .SetProperty(j => j.Attempts, j => j.Attempts + 1)
                        .SetProperty(j => j.ProcessedAtUtc, nowUtc)
                        .SetProperty(j => j.LastError, error)
                        .SetProperty(j => j.NextAttemptAtUtc, terminal ? (DateTime?)null : nowUtc.Add(retryDelay))
                        .SetProperty(j => j.ClaimToken, (string?)null),
                    cancellationToken);
        }

        public async Task CleanupTerminalAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
        {
            await _context.DispatchJobs
                .Where(j => (j.Status == DispatchJobStatus.Succeeded || j.Status == DispatchJobStatus.Failed)
                    && j.ProcessedAtUtc != null
                    && j.ProcessedAtUtc < olderThanUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetDispatchedBillingTypesAsync(
            IReadOnlyCollection<int> sessionIds,
            CancellationToken cancellationToken)
        {
            var ids = sessionIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, IReadOnlyList<string>>();

            var rows = await _context.DispatchJobs
                .AsNoTracking()
                .Where(j => ids.Contains(j.SessionId)
                    && (j.Status == DispatchJobStatus.Pending
                        || j.Status == DispatchJobStatus.Processing
                        || j.Status == DispatchJobStatus.Succeeded)
                    && j.DispatchType.StartsWith("BillingFollowUp:"))
                .Select(j => new { j.SessionId, j.DispatchType })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.SessionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<string>)g.Select(r => r.DispatchType).Distinct().OrderBy(t => t).ToList());
        }
    }
}