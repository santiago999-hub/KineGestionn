using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IProfessionalRepository
    {
        Task<Professional?> GetByIdAsync(int id);
        Task<IEnumerable<Professional>> GetAllAsync();
        Task<IEnumerable<Professional>> GetActivosAsync();
        Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<int> CountActiveAsync();
        Task<bool> ExistsByMatriculaAsync(string matricula, int? excludeId = null);
        Task<Professional> AddAsync(Professional professional);
        Task<Professional> UpdateAsync(Professional professional);
        Task DeleteAsync(int id);
    }
}
