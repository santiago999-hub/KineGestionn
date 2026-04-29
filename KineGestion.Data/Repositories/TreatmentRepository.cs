using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;

namespace KineGestion.Data.Repositories
{
    public class TreatmentRepository : ITreatmentRepository
    {
        private readonly AppDbContext _context;

        public TreatmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Treatment?> GetByIdAsync(int id)
            => await _context.Treatments
                             .Include(t => t.Patient)
                             .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<IEnumerable<Treatment>> GetAllAsync()
            => await _context.Treatments
                             .AsNoTracking()
                             .Include(t => t.Patient)
                             .Include(t => t.Sesiones)
                             .ToListAsync();

        public async Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId)
            => await _context.Treatments
                             .AsNoTracking()
                             .Where(t => t.PatientId == patientId)
                             .Include(t => t.Patient)
                             .ToListAsync();

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await _context.Treatments
                             .AsNoTracking()
                             .CountAsync(t => t.PatientId == patientId);

        public async Task<(IEnumerable<Treatment> Treatments, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            var query = _context.Treatments
                .AsNoTracking()
                .Include(t => t.Patient)
                .Include(t => t.Sesiones)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(t =>
                    t.Descripcion.Contains(term) ||
                    (t.Patient != null && (t.Patient.Nombre + " " + t.Patient.Apellido).Contains(term)));
            }

            query = query.OrderBy(t => t.FechaInicio);

            int totalCount = await query.CountAsync();
            var treatments = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (treatments, totalCount);
        }

        public async Task<Treatment> AddAsync(Treatment treatment)
        {
            _context.Treatments.Add(treatment);
            await _context.SaveChangesAsync();
            return treatment;
        }

        public async Task<Treatment> UpdateAsync(Treatment treatment)
        {
            _context.Treatments.Update(treatment);
            await _context.SaveChangesAsync();
            return treatment;
        }

        public async Task DeleteAsync(int id)
        {
            var treatment = await _context.Treatments.FindAsync(id);
            if (treatment is not null)
            {
                _context.Treatments.Remove(treatment);
                await _context.SaveChangesAsync();
            }
        }
    }
}
