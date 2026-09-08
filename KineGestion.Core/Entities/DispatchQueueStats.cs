namespace KineGestion.Core.Entities
{
    /// <summary>
    /// Snapshot de conteos de la cola de despachos para la vista de operación.
    /// </summary>
    public sealed class DispatchQueueStats
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public int CancelledCount { get; set; }

        /// <summary>Jobs Pending con CreatedAtUtc anterior al umbral: el worker no los está tomando.</summary>
        public int StuckCount { get; set; }
    }
}