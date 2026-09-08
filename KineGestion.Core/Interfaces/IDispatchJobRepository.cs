using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IDispatchJobRepository
    {
        /// <summary>Busca un job abierto (Pending/Processing) con la misma clave de deduplicación.</summary>
        Task<DispatchJob?> FindOpenAsync(int sessionId, string dispatchType, string payloadHash, CancellationToken cancellationToken);

        Task AddAsync(DispatchJob job, CancellationToken cancellationToken);

        /// <summary>
        /// Reclama atómicamente el siguiente lote de jobs elegibles:
        /// Pending con NextAttemptAtUtc vencido, o Processing cuyo lease de claim ya expiró.
        /// </summary>
        Task<IReadOnlyList<DispatchJob>> ClaimNextBatchAsync(int batchSize, TimeSpan leaseDuration, DateTime nowUtc, CancellationToken cancellationToken);

        Task MarkSucceededAsync(int id, string? resultJson, DateTime nowUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Registra un intento fallido. Al alcanzar maxAttempts el job pasa a Failed (terminal);
        /// en caso contrario vuelve a Pending con retry con backoff.
        /// </summary>
        Task MarkFailedAsync(int id, string error, TimeSpan retryDelay, int maxAttempts, DateTime nowUtc, CancellationToken cancellationToken);

        Task CleanupTerminalAsync(DateTime olderThanUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Devuelve, por sesión, los DispatchType de cobranza ("BillingFollowUp:&lt;Tier&gt;") ya
        /// despachados o pendientes, para que la automatización D+1 escale de nivel sin reenviar.
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetDispatchedBillingTypesAsync(IReadOnlyCollection<int> sessionIds, CancellationToken cancellationToken);

        /// <summary>Contadores por estado + jobs pendientes estancados para la vista de operación.</summary>
        Task<DispatchQueueStats> GetStatsAsync(DateTime stuckThresholdUtc, CancellationToken cancellationToken);

        /// <summary>Página de jobs con filtros por estado, tipo de despacho y texto (sesión/payload).</summary>
        Task<(IReadOnlyList<DispatchJob> Items, int TotalCount)> GetJobsAsync(
            DispatchJobStatus? status,
            string? dispatchType,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        /// <summary>Tipos de despacho distintos para el filtro de la vista.</summary>
        Task<IReadOnlyList<string>> GetDistinctDispatchTypesAsync(CancellationToken cancellationToken);

        /// <summary>Vuelve a cola (Pending, intentos en 0) los jobs Failed indicados. Devuelve cuántos reintentó.</summary>
        Task<int> ResetForRetryAsync(IReadOnlyCollection<int> ids, DateTime nowUtc, CancellationToken cancellationToken);

        /// <summary>Marca como Cancelled (terminal) los jobs abiertos (Pending/Processing) indicados. Devuelve cuántos canceló.</summary>
        Task<int> CancelAsync(IReadOnlyCollection<int> ids, DateTime nowUtc, CancellationToken cancellationToken);
    }
}