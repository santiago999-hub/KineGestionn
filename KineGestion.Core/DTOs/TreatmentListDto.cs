using System;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// DTO de solo lectura para el listado paginado de tratamientos.
    /// Evita cargar la colección completa de Sesiones solo para contar cuántas hay.
    /// El conteo se resuelve como subquery SQL en lugar de traer todos los objetos.
    /// </summary>
    public record TreatmentListDto(
        int Id,
        string Descripcion,
        int CantidadSesionesTotales,
        DateTime FechaInicio,
        int PatientId,
        string PatientNombre,
        int SesionesCount
    );
}
