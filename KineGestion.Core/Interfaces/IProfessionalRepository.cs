using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IProfessionalRepository
    {
        Task<Professional?> GetByIdAsync(int id);
        Task<IEnumerable<Professional>> GetAllAsync();
        Task<bool> ExistsByMatriculaAsync(string matricula, int? excludeId = null);
        Task<Professional> AddAsync(Professional professional);
        Task<Professional> UpdateAsync(Professional professional);
        Task DeleteAsync(int id);
    }
}
