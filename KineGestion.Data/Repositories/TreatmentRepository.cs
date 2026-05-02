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

        /// <summary>
        /// Solo Id+Descripcion para dropdowns. No hace Include ni carga nav properties.
        /// Genera: SELECT Id, Descripcion FROM Treatments ORDER BY Descripcion
        /// </summary>
        public async Task<IEnumerable<TreatmentSelectDto>> GetForSelectAsync()
            => await _context.Treatments
                             .AsNoTracking()
                             .OrderBy(t => t.Descripcion)
                             .Select(t => new TreatmentSelectDto(t.Id, t.Descripcion))
                             .ToListAsync();

        public async Task<IEnumerable<TreatmentSelectDto>> GetByPatientForSelectAsync(int patientId)
            => await _context.Treatments
                             .AsNoTracking()
                             .Where(t => t.PatientId == patientId)
                             .OrderBy(t => t.Descripcion)
                             .Select(t => new TreatmentSelectDto(t.Id, t.Descripcion))
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

        /// <summary>
        /// Proyección optimizada: resuelve el conteo de sesiones como subquery SQL
        /// sin cargar los objetos Session en memoria.
        /// Antes: Include(t => t.Sesiones) cargaba todos los objetos de sesión.
        /// Ahora: t.Sesiones.Count() se traduce a COUNT(*) con subquery en SQL.
        /// </summary>
        public async Task<(IEnumerable<TreatmentListDto> Items, int TotalCount)> GetPagedListAsync(int page, int pageSize, string? search)
        {
            var query = _context.Treatments.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(t =>
                    t.Descripcion.Contains(term) ||
                    (t.Patient != null && (t.Patient.Nombre + " " + t.Patient.Apellido).Contains(term)));
            }

            query = query.OrderBy(t => t.FechaInicio);

            int totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TreatmentListDto(
                    t.Id,
                    t.Descripcion,
                    t.CantidadSesionesTotales,
                    t.FechaInicio,
                    t.PatientId,
                    t.Patient != null ? t.Patient.Apellido + ", " + t.Patient.Nombre : string.Empty,
                    t.Sesiones.Count()
                ))
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountAsync()
            => await _context.Treatments.AsNoTracking().CountAsync();

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
