using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de repositorio para Patient.
    /// KineGestion.Data implementará esta interfaz con EF Core.
    /// KineGestion.Core no conoce nada de la base de datos.
    /// </summary>
    public interface IPatientRepository
    {
        Task<Patient?> GetByIdAsync(int id);
        Task<IEnumerable<Patient>> GetAllAsync();
        Task<IEnumerable<Patient>> GetActivosAsync();
        Task<bool> ExistsByDniAsync(string dni, int? excludeId = null);
        Task<Patient> AddAsync(Patient patient);
        Task<Patient> UpdateAsync(Patient patient);
        Task DeleteAsync(int id);
    }
}
