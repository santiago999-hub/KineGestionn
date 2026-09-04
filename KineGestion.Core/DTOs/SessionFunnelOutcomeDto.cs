using System;
using KineGestion.Core;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Resultado de una sesión que recibió un recordatorio, para el embudo de
    /// recordatorios. La clasificación de confirmación/cancelación se hace sobre
    /// las notas internas descifradas por EF (InternalNotes no es consultable en SQL).
    /// </summary>
    public record SessionFunnelOutcomeDto(
        int SessionId,
        SessionStatus Status,
        bool ConfirmedByPatient,
        bool CanceledByPatient,
        DateTime? CancelledAtUtc);
}