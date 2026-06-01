using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
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

        public async Task<OfficeClinicalProfileDto?> GetClinicalProfileAsync(int officeId)
        {
            var office = await _context.Offices
                .AsNoTracking()
                .Include(o => o.Equipments)
                .FirstOrDefaultAsync(o => o.Id == officeId);

            if (office is null)
                return null;

            var sessionBaseQuery = _context.Sessions
                .AsNoTracking()
                .Where(s => s.OfficeId == officeId);

            var professionalRows = await sessionBaseQuery
                .Where(s => s.Professional != null)
                .Select(s => new
                {
                    s.Professional!.Apellido,
                    s.Professional!.Nombre
                })
                .ToListAsync();

            var professionals = professionalRows
                .Select(row => BuildProfessionalDisplayName(row.Apellido, row.Nombre))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var treatmentRows = await sessionBaseQuery
                .Select(s => s.Treatment != null ? s.Treatment.Descripcion : string.Empty)
                .ToListAsync();

            var treatments = NormalizeDistinctSorted(treatmentRows);

            var obraSocialRows = await sessionBaseQuery
                .Select(s => s.Patient != null ? s.Patient.ObraSocial : string.Empty)
                .ToListAsync();

            var obrasSociales = NormalizeDistinctSorted(obraSocialRows);

            var equipments = NormalizeDistinctSorted(office.Equipments.Select(e => e.Name));

            return new OfficeClinicalProfileDto
            {
                OfficeId = officeId,
                Professionals = professionals,
                Treatments = treatments,
                Equipments = equipments,
                ObrasSociales = obrasSociales
            };
        }

        private static List<string> NormalizeDistinctSorted(IEnumerable<string?> values)
            => values
                .Select(value => value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string BuildProfessionalDisplayName(string? lastName, string? firstName)
        {
            var normalizedLastName = lastName?.Trim();
            var normalizedFirstName = firstName?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedLastName) && string.IsNullOrWhiteSpace(normalizedFirstName))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedLastName))
                return normalizedFirstName!;

            if (string.IsNullOrWhiteSpace(normalizedFirstName))
                return normalizedLastName;

            return $"{normalizedLastName}, {normalizedFirstName}";
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
