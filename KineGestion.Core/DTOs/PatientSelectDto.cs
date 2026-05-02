namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// DTO mínimo para poblar dropdowns de pacientes sin cargar
    /// ObraSocial, FechaNacimiento ni navigation properties.
    /// </summary>
    public record PatientSelectDto(int Id, string Nombre, string Apellido, string DNI);
}
