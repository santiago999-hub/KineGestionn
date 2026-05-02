using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de repositorio para Patient.
    /// KineGestion.Core no conoce nada de la base de datos.
    /// </summary>
    public interface IPatientRepository
    {
        Task<Patient?> GetByIdAsync(int id);
        Task<IEnumerable<Patient>> GetAllAsync();
        Task<IEnumerable<Patient>> GetActivosAsync();
        Task<IEnumerable<PatientSelectDto>> GetForSelectAsync();
        Task<(IEnumerable<Patient> Patients, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<int> CountActiveAsync();
        Task<bool> ExistsByDniAsync(string dni, int? excludeId = null);
        Task<Patient> AddAsync(Patient patient);
        Task<Patient> UpdateAsync(Patient patient);
        Task DeleteAsync(int id);
    }
}
