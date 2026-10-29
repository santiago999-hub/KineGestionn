using System;

namespace KineGestion.Core.DTOs
{
    public record PendingPatientDebtDto(
        int PatientId,
        string PatientLabel,
        string? Telefono,
        int PendingCount,
        DateTime OldestPendingUtc);
}
