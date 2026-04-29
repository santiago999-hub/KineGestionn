using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de servicio para la gestión de pacientes.
    /// Permite desacoplar la lógica de negocio de la implementación concreta
    /// y habilita la inyección de dependencias en controladores y servicios.
    /// </summary>
    public interface IPatientService
    {
        /// <summary>Obtiene un paciente por su identificador único.</summary>
        Task<Patient?> GetByIdAsync(int id);

        /// <summary>Retorna la lista completa de pacientes registrados.</summary>
        Task<IEnumerable<Patient>> GetAllAsync();

        /// <summary>Retorna solo los pacientes con IsActivo = true.</summary>
        Task<IEnumerable<Patient>> GetActivePatientsAsync();

        /// <summary>Retorna una página de pacientes activos con búsqueda opcional.</summary>
        Task<(IEnumerable<Patient> Patients, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);

        /// <summary>Cuenta los pacientes activos sin cargarlos en memoria.</summary>
        Task<int> CountActiveAsync();

        /// <summary>
        /// Valida que el DNI no esté registrado por otro paciente.
        /// Acepta excludeId para el caso de edición (no comparar consigo mismo).
        /// Lanza InvalidOperationException si el DNI ya existe.
        /// </summary>
        Task ValidateDniUniquenessAsync(string dni, int? excludeId = null);

        /// <summary>Crea un nuevo paciente. Valida DNI único antes de persistir.</summary>
        Task<Patient> CreateAsync(Patient patient);

        /// <summary>Actualiza los datos de un paciente existente. Valida DNI único antes de persistir.</summary>
        Task<Patient> UpdateAsync(Patient patient);

        /// <summary>Elimina un paciente por su identificador.</summary>
        Task DeleteAsync(int id);
    }
}
