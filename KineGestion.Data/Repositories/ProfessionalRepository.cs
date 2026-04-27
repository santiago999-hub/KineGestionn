using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task<Professional?> GetByIdAsync(int id)
            => await _context.Professionals.FindAsync(id);

        public async Task<IEnumerable<Professional>> GetAllAsync()
            => await _context.Professionals.AsNoTracking().ToListAsync();

        public async Task<bool> ExistsByMatriculaAsync(string matricula, int? excludeId = null)
            => await _context.Professionals
                             .AsNoTracking()
                             .AnyAsync(p => p.Matricula == matricula && p.Id != excludeId);

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
                _context.Professionals.Remove(professional);
                await _context.SaveChangesAsync();
            }
        }
    }
}
