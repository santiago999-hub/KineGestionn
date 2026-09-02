using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Data.Repositories
{
    /// <summary>
    /// Implementación concreta de IEquipmentRepository usando EF Core.
    /// Sabe CÓMO acceder a la base de datos. No sabe nada de reglas de negocio.
    /// </summary>
    public class EquipmentRepository : IEquipmentRepository
    {
        private readonly AppDbContext _context;

        public EquipmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Equipment?> GetByIdAsync(int id)
            => await _context.Equipments
                .AsNoTracking()
                .Include(e => e.Office)
                .FirstOrDefaultAsync(e => e.Id == id);

        public async Task<IEnumerable<Equipment>> GetAllAsync()
            => await _context.Equipments
                .AsNoTracking()
                .Include(e => e.Office)
                .OrderBy(e => e.Name)
                .ToListAsync();

        public async Task<(IEnumerable<Equipment> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, int? officeId)
        {
            var query = _context.Equipments.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.Name.Contains(search));

            if (officeId.HasValue)
                query = query.Where(e => e.OfficeId == officeId.Value);

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(e => e.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(e => e.Office)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<Equipment>> GetByOfficeIdAsync(int officeId)
            => await _context.Equipments
                .AsNoTracking()
                .Where(e => e.OfficeId == officeId)
                .OrderBy(e => e.Name)
                .ToListAsync();

        public async Task<bool> ExistsByNameAsync(string name, int? officeId, int? excludeId = null)
        {
            var query = _context.Equipments.AsNoTracking().AsQueryable();

            if (officeId.HasValue)
                query = query.Where(e => e.OfficeId == officeId.Value);

            return await query.AnyAsync(e => e.Name.ToLower() == name.ToLower()
                                             && (excludeId == null || e.Id != excludeId));
        }

        public async Task<int> CountByOfficeIdAsync(int officeId)
            => await _context.Equipments
                .AsNoTracking()
                .CountAsync(e => e.OfficeId == officeId);

        public async Task<Equipment> AddAsync(Equipment equipment)
        {
            _context.Equipments.Add(equipment);
            await _context.SaveChangesAsync();
            return equipment;
        }

        public async Task<Equipment> UpdateAsync(Equipment equipment)
        {
            _context.Equipments.Update(equipment);
            await _context.SaveChangesAsync();
            return equipment;
        }

        public async Task DeleteAsync(int id)
        {
            var equipment = await _context.Equipments.FindAsync(id);
            if (equipment is not null)
            {
                _context.Equipments.Remove(equipment);
                await _context.SaveChangesAsync();
            }
        }
    }
}