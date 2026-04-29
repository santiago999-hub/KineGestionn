using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IOfficeRepository
    {
        Task<Office?> GetByIdAsync(int id);
        Task<IEnumerable<Office>> GetAllAsync();
        Task<IEnumerable<Office>> GetActiveAsync();
        Task<(IEnumerable<Office> Offices, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<Office> AddAsync(Office office);
        Task<Office> UpdateAsync(Office office);
        Task DeleteAsync(int id);
    }
}
