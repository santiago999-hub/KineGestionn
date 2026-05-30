using System;

namespace KineGestion.Core.DTOs
{
    public record SessionReminderCandidateDto(
        int SessionId,
        DateTime FechaHora,
        string PacienteNombre,
        string? PacienteEmail,
        string? PacienteTelefono,
        string ProfesionalNombre,
        string? TratamientoDescripcion
    );
}