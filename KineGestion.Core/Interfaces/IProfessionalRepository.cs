using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IProfessionalRepository
    {
        Task<Professional?> GetByIdAsync(int id);
        /// <summary>OBSOLETO: carga todos los profesionales en memoria. Usar GetPagedAsync o GetForSelectAsync.</summary>
        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync o GetForSelectAsync para dropdowns.")]
        Task<IEnumerable<Professional>> GetAllAsync();
        Task<IEnumerable<Professional>> GetActivosAsync();
        Task<IEnumerable<ProfessionalSelectDto>> GetForSelectAsync();
        Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<int> CountActiveAsync();
        Task<bool> ExistsByMatriculaAsync(string matricula, int? excludeId = null);
        Task<Professional> AddAsync(Professional professional);
        Task<Professional> UpdateAsync(Professional professional);
        Task DeleteAsync(int id);
    }
}
