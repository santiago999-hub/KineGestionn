using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ITreatmentRepository
    {
        Task<Treatment?> GetByIdAsync(int id);
        /// <summary>OBSOLETO: carga todos los tratamientos con Include de Patient y Sesiones. Usar GetPagedListAsync.</summary>
        [Obsolete("Carga toda la tabla con nav properties en memoria. Usar GetPagedListAsync.")]
        Task<IEnumerable<Treatment>> GetAllAsync();
        Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId);
        /// <summary>Carga entidades completas con 2 JOINs. Usar <see cref="GetPagedListAsync"/>.</summary>
        [Obsolete("Carga entidades con Patient + Sesiones en memoria. Usar GetPagedListAsync.")]
        Task<(IEnumerable<Treatment> Treatments, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        /// <summary>
        /// Proyección optimizada para listados: resuelve el conteo de sesiones
        /// como subquery SQL sin cargar los objetos de sesión en memoria.
        /// </summary>
        Task<(IEnumerable<TreatmentListDto> Items, int TotalCount)> GetPagedListAsync(int page, int pageSize, string? search);
        /// <summary>Proyecta solo Id+Descripcion para poblar dropdowns. Sin Include.</summary>
        Task<IEnumerable<TreatmentSelectDto>> GetForSelectAsync();
        Task<IEnumerable<TreatmentSelectDto>> GetByPatientForSelectAsync(int patientId);
        Task<int> CountAsync();
        Task<int> CountByPatientIdAsync(int patientId);
        Task<Treatment> AddAsync(Treatment treatment);
        Task<Treatment> UpdateAsync(Treatment treatment);
        Task DeleteAsync(int id);
    }
}
