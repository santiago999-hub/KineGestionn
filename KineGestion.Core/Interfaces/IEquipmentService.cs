using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de servicio para Equipment.
    /// Contiene las reglas de negocio: validación de nombre duplicado por consultorio,
    /// restricciones de eliminación, etc.
    /// </summary>
    public interface IEquipmentService
    {
        Task<Equipment?> GetByIdAsync(int id);
        Task<IEnumerable<Equipment>> GetAllAsync();
        Task<(IEnumerable<Equipment> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, int? officeId);
        Task<IEnumerable<Equipment>> GetByOfficeIdAsync(int officeId);
        Task<Equipment> CreateAsync(Equipment equipment);
        Task<Equipment> UpdateAsync(Equipment equipment);
        Task DeleteAsync(int id);
    }
}