using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IOfficeRepository
    {
        Task<Office?> GetByIdAsync(int id);
        /// <summary>OBSOLETO: carga todos los consultorios en memoria. Usar GetPagedAsync o GetActiveAsync.</summary>
        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync o GetActiveAsync.")]
        Task<IEnumerable<Office>> GetAllAsync();
        Task<IEnumerable<Office>> GetActiveAsync();
        Task<(IEnumerable<Office> Offices, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<OfficeClinicalProfileDto?> GetClinicalProfileAsync(int officeId);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<Office> AddAsync(Office office);
        Task<Office> UpdateAsync(Office office);
        Task DeleteAsync(int id);
    }
}
