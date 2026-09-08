using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Actualizaciones masivas por set (ExecuteUpdate) sobre sesiones.
    /// Devuelve cuántas sesiones se actualizaron y cuántas se saltaron por no cumplir
    /// la condición (el conteo de saltos permite al caller informar la diferencia).
    /// </summary>
    public interface ISessionBatchRepository
    {
        Task<(int UpdatedCount, int SkippedCount)> MarkCompletedPendingAsPaidBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc);
        Task<(int UpdatedCount, int SkippedCount)> MarkPaidAsPendingBatchAsync(IReadOnlyCollection<int> sessionIds, DateTime actionAtUtc);
    }
}