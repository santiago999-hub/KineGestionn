using System;
using KineGestion.Core;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Sesión de la agenda del día con los datos mínimos para mostrar la línea de tiempo.
    /// </summary>
    public record SessionAgendaItemDto(
        int SessionId,
        DateTime FechaHora,
        string PacienteNombre,
        string ProfesionalNombre,
        string? OfficeNombre,
        SessionStatus Status,
        PaymentStatus PaymentStatus);
}
