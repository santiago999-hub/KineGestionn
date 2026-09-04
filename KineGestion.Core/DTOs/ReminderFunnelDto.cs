using System;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Embudo de recordatorios: enviados con éxito → confirmados por el paciente
    /// (link "confirm") → asistidos (sesión completada). Sirve de baseline para
    /// experimentación A/B de plantillas de mensajes.
    /// </summary>
    public record ReminderFunnelDto(
        int Sent,
        int Confirmed,
        int Attended)
    {
        /// <summary>Cancelado por el paciente vía el link del recordatorio, o sesión cancelada.</summary>
        public int Canceled { get; init; }

        /// <summary>Tasa de confirmación = confirmados / enviados.</summary>
        public decimal ConfirmationRate
            => Sent > 0
                ? Math.Round((decimal)Confirmed * 100m / Sent, 2)
                : 0m;

        /// <summary>Tasa de asistencia = asistidos / enviados.</summary>
        public decimal AttendanceRate
            => Sent > 0
                ? Math.Round((decimal)Attended * 100m / Sent, 2)
                : 0m;

        /// <summary>Tasa de cancelación vía recordatorio = cancelados / enviados.</summary>
        public decimal CancellationRate
            => Sent > 0
                ? Math.Round((decimal)Canceled * 100m / Sent, 2)
                : 0m;
    }
}