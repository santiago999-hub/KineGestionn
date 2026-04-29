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
                .Include(o => o.Equipments)
                .FirstOrDefaultAsync(o => o.Id == id);

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
