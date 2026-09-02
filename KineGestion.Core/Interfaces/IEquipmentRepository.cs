using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de repositorio para Equipment.
    /// KineGestion.Core no conoce nada de la base de datos.
    /// </summary>
    public interface IEquipmentRepository
    {
        Task<Equipment?> GetByIdAsync(int id);
        Task<IEnumerable<Equipment>> GetAllAsync();
        Task<(IEnumerable<Equipment> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, int? officeId);
        Task<IEnumerable<Equipment>> GetByOfficeIdAsync(int officeId);
        Task<bool> ExistsByNameAsync(string name, int? officeId, int? excludeId = null);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<Equipment> AddAsync(Equipment equipment);
        Task<Equipment> UpdateAsync(Equipment equipment);
        Task DeleteAsync(int id);
    }
}