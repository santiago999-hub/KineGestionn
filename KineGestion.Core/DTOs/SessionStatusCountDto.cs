using KineGestion.Core;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Distribución de sesiones por estado dentro de un rango.
    /// </summary>
    public record SessionStatusCountDto(SessionStatus Status, int Count);
}
