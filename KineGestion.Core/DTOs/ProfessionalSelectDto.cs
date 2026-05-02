namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// DTO mínimo para poblar dropdowns de profesionales sin cargar
    /// campos no necesarios para la selección.
    /// </summary>
    public record ProfessionalSelectDto(int Id, string Nombre, string Apellido, string Matricula);
}
