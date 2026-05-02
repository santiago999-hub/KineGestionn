using System;
using KineGestion.Core;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// DTO de solo lectura para el listado paginado de sesiones (admin y profesional).
    /// Proyecta solo los campos necesarios para la tabla — sin cargar objetos completos
    /// de Patient, Professional, Treatment ni Office como navigation properties.
    /// Evolution se excluye: solo se carga bajo demanda en la vista de detalle.
    /// </summary>
    public record SessionListDto(
        int Id,
        DateTime FechaHora,
        SessionStatus Status,
        PaymentStatus PaymentStatus,
        int NroSesionEnTratamiento,
        string PacienteNombre,
        string ProfesionalNombre,
        string? TratamientoDescripcion,
        string? OfficeNombre,
        bool EvolutionBloqueada
    );
}
