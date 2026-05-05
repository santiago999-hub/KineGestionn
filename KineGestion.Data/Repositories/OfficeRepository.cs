using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;

namespace KineGestion.Data.Repositories
{
    public class OfficeRepository : IOfficeRepository
    {
        private readonly AppDbContext _context;

        public OfficeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Office?> GetByIdAsync(int id)
            => await _context.Offices
                .AsNoTracking()
                .Include(o => o.Equipments)
                .FirstOrDefaultAsync(o => o.Id == id);

        /// <summary>OBSOLETO: carga todos los consultorios con Equipments. Usar GetPagedAsync o GetActiveAsync.</summary>
        [Obsolete("Carga toda la tabla con nav properties en memoria. Usar GetPagedAsync o GetActiveAsync.")]
        public async Task<IEnumerable<Office>> GetAllAsync()
            => await _context.Offices
                .Include(o => o.Equipments)
                .OrderBy(o => o.Name)
                .ToListAsync();

        public async Task<IEnumerable<Office>> GetActiveAsync()
            => await _context.Offices
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .ToListAsync();

        public async Task<(IEnumerable<Office> Offices, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            // AsNoTracking: el listado es solo lectura; los Equipments no se muestran en la tabla.
            var query = _context.Offices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => o.Name.Contains(search));

            int totalCount = await query.CountAsync();

            var offices = await query
                .OrderBy(o => o.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // sin Include: la vista Index solo usa Name e IsActive

            return (offices, totalCount);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
            => await _context.Offices
                .AnyAsync(o => o.Name.ToLower() == name.ToLower()
                               && (excludeId == null || o.Id != excludeId));

        public async Task<Office> AddAsync(Office office)
        {
            _context.Offices.Add(office);
            await _context.SaveChangesAsync();
            return office;
        }

        public async Task<Office> UpdateAsync(Office office)
        {
            _context.Offices.Update(office);
            await _context.SaveChangesAsync();
            return office;
        }

        public async Task DeleteAsync(int id)
        {
            var office = await _context.Offices.FindAsync(id);
            if (office is not null)
            {
                _context.Offices.Remove(office);
                await _context.SaveChangesAsync();
            }
        }
    }
}
