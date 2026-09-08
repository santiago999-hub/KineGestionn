using System;

namespace KineGestion.Core
{
    /// <summary>
    /// Formato único de las notas de sistema escritas en Session.InternalNotes.
    /// Tanto el camino individual (SessionService.AppendSystemNote) como el camino
    /// batch por set (SessionBatchRepository.ExecuteUpdate) usan esta fuente para que
    /// el historial nunca se desincronice entre vías.
    /// </summary>
    public static class SessionNotes
    {
        public const string ConfirmedByPatient = "CONFIRMADA_PACIENTE";
        public const string CanceledByPatient = "CANCELADA_PACIENTE";
        public const string PaymentRegistered = "COBRO_REGISTRADO";
        public const string PaymentReopened = "COBRO_REABIERTO";

        public static string CanceledByReason(CancellationReason reason)
            => $"CANCELADA_MOTIVO_{reason}";

        /// <summary>
        /// Formato de las notas: <c>[yyyy-MM-dd HH:mm 'UTC'] ACCION</c>.
        /// </summary>
        public static string Format(DateTime utcNow, string action)
            => $"[{utcNow:yyyy-MM-dd HH:mm 'UTC'}] {action}";

        /// <summary>
        /// Appends the nota al historial existente (primera nota = solo nota).
        /// Uso en memoria (SessionService); los paths batch replican la concatenación
        /// en la expresión traducible de ExecuteUpdate sin invocar a este método.
        /// </summary>
        public static string Append(string? existingNotes, string note)
            => string.IsNullOrWhiteSpace(existingNotes)
                ? note
                : existingNotes + Environment.NewLine + note;
    }
}