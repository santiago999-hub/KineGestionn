using System;

namespace KineGestion.Core.DTOs
{
    public record BillingFollowUpCandidateDto(
        int SessionId,
        DateTime FechaHora,
        string PacienteNombre,
        string? PacienteEmail,
        string? PacienteTelefono,
        string ProfesionalNombre,
        string? TratamientoDescripcion,
        int PaymentAgeDays
    );
}
