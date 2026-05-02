using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IProfessionalService
    {
        Task<Professional?> GetByIdAsync(int id);
        Task<IEnumerable<Professional>> GetAllAsync();
        Task<IEnumerable<Professional>> GetActiveProfessionalsAsync();
        /// <summary>Proyección mínima (Id, Nombre, Apellido, Matricula) para dropdowns.</summary>
        Task<IEnumerable<ProfessionalSelectDto>> GetForSelectAsync();
        Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<int> CountActiveAsync();
        Task ValidateMatriculaUniquenessAsync(string matricula, int? excludeId = null);
        Task<Professional> CreateAsync(Professional professional);
        Task<Professional> UpdateAsync(Professional professional);
        Task DeleteAsync(int id);
    }
}
