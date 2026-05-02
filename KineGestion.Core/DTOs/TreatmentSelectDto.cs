namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// DTO mínimo para poblar listas desplegables de tratamientos.
    /// Solo trae Id y Descripcion — evita cargar Patient ni Sesiones.
    /// </summary>
    public record TreatmentSelectDto(int Id, string Descripcion);
}
