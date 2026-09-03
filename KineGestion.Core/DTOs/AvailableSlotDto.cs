using System;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Turno alternativo sugerido para la reprogramación (recaptura) de una sesión cancelada.
    /// </summary>
    public record AvailableSlotDto(
        DateTime FechaHora,
        string Display
    );
}
