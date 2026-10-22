using System;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Punto de la serie diaria de sesiones: total, completadas, canceladas y pagadas del día.
    /// </summary>
    public record DailySessionCountDto(DateTime Day, int Total, int Completed, int Canceled, int Paid);
}
