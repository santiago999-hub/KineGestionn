using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;

namespace KineGestion.Data.Repositories
{
    public class ProfessionalRepository : IProfessionalRepository
    {
        private readonly AppDbContext _context;

        public ProfessionalRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// AsNoTracking: vistas de detalle y edición solo necesitan leer el registro.
        /// UpdateAsync llama explícitamente a _context.Update(), por lo que el tracking no es necesario aquí.
        /// </summary>
        public async Task<Professional?> GetByIdAsync(int id)
            => await _context.Professionals
                             .AsNoTracking()
                             .FirstOrDefaultAsync(p => p.Id == id);

        /// <summary>OBSOLETO: carga la tabla completa en memoria. Usar GetPagedAsync o GetForSelectAsync.</summary>
        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync o GetForSelectAsync.")]
        public async Task<IEnumerable<Professional>> GetAllAsync()
            => await _context.Professionals.AsNoTracking().ToListAsync();

        /// <summary>Lista a los profesionales activos (el filtro global aplica a la entidad raíz).</summary>
        public async Task<IEnumerable<Professional>> GetActivosAsync()
            => await _context.Professionals.AsNoTracking().ToListAsync();

        /// <summary>
        /// Proyección mínima para dropdowns: trae solo Id, Nombre, Apellido y Matricula.
        /// </summary>
        public async Task<IEnumerable<ProfessionalSelectDto>> GetForSelectAsync()
            => await _context.Professionals
                             .AsNoTracking()
                             .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre)
                             .Select(p => new ProfessionalSelectDto(p.Id, p.Nombre, p.Apellido, p.Matricula))
                             .ToListAsync();

        /// <summary>
        /// Verifica matrícula duplicada incluyendo profesionales desactivados
        /// (IgnoreQueryFilters) para preservar la unicidad integra tras el borrado lógico.
        /// </summary>
        public async Task<bool> ExistsByMatriculaAsync(string matricula, int? excludeId = null)
            => await _context.Professionals
                             .IgnoreQueryFilters()
                             .AsNoTracking()
                             .AnyAsync(p => p.Matricula == matricula && p.Id != excludeId);

        public async Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            var query = _context.Professionals.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    (p.Nombre + " " + p.Apellido).Contains(term) ||
                    p.Matricula.Contains(term) ||
                    p.Especialidad.Contains(term));
            }

            query = query.OrderBy(p => p.Apellido).ThenBy(p => p.Nombre);

            int totalCount = await query.CountAsync();
            var professionals = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (professionals, totalCount);
        }

        public async Task<int> CountActiveAsync()
            => await _context.Professionals.AsNoTracking().CountAsync();

        public async Task<Professional> AddAsync(Professional professional)
        {
            _context.Professionals.Add(professional);
            await _context.SaveChangesAsync();
            return professional;
        }

        public async Task<Professional> UpdateAsync(Professional professional)
        {
            _context.Professionals.Update(professional);
            await _context.SaveChangesAsync();
            return professional;
        }

        public async Task DeleteAsync(int id)
        {
            var professional = await _context.Professionals.FindAsync(id);
            if (professional is not null)
            {
                professional.IsActivo = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}
