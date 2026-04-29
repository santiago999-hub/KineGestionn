using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IProfessionalService
    {
        Task<Professional?> GetByIdAsync(int id);
        Task<IEnumerable<Professional>> GetAllAsync();
        Task<IEnumerable<Professional>> GetActiveProfessionalsAsync();
        Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task ValidateMatriculaUniquenessAsync(string matricula, int? excludeId = null);
        Task<Professional> CreateAsync(Professional professional);
        Task<Professional> UpdateAsync(Professional professional);
        Task DeleteAsync(int id);
    }
}
