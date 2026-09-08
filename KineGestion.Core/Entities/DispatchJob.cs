using System;

namespace KineGestion.Core.Entities
{
    public enum DispatchJobStatus
    {
        Pending = 0,
        Processing = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Cola durable (outbox) de despachos de recordatorios/cobranzas.
    /// Persistida en BD para que ningún envío se pierda si el proceso muere.
    /// El worker reclama filas con status Pending (o Processing con lease vencido)
    /// y las marca Succeeded/Failed tras procesarlas.
    /// </summary>
    public class DispatchJob
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string DispatchType { get; set; } = string.Empty;

        /// <summary>Payload serializado (ReminderDispatchWorkItem).</summary>
        public string PayloadJson { get; set; } = string.Empty;

        /// <summary>Hash canónico del contenido; permite deduplicar envíos idénticos.</summary>
        public string PayloadHash { get; set; } = string.Empty;

        public DispatchJobStatus Status { get; set; }
        public int Attempts { get; set; }
        public string? ClaimToken { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ClaimedAtUtc { get; set; }
        public DateTime? NextAttemptAtUtc { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
        public string? ResultJson { get; set; }
        public string? LastError { get; set; }
    }
}