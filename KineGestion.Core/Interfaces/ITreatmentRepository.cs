using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ITreatmentRepository
    {
        Task<Treatment?> GetByIdAsync(int id);
        Task<IEnumerable<Treatment>> GetAllAsync();
        Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId);
        Task<Treatment> AddAsync(Treatment treatment);
        Task<Treatment> UpdateAsync(Treatment treatment);
        Task DeleteAsync(int id);
    }
}
