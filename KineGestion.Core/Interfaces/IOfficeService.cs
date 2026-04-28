using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IOfficeService
    {
        Task<Office?> GetByIdAsync(int id);
        Task<IEnumerable<Office>> GetAllAsync();
        Task<IEnumerable<Office>> GetActiveAsync();
        Task<Office> CreateAsync(Office office);
        Task<Office> UpdateAsync(Office office);
        Task DeleteAsync(int id);
    }
}
