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
    /// <summary>
    /// Implementación concreta de IPatientRepository usando EF Core.
    /// Sabe CÓMO acceder a la base de datos. No sabe nada de reglas de negocio.
    /// KineGestion.Core solo conoce la interfaz, nunca esta clase directamente.
    /// </summary>
    public class PatientRepository : IPatientRepository
    {
        private readonly AppDbContext _context;

        public PatientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Patient?> GetByIdAsync(int id)
            => await _context.Patients.FindAsync(id);

        public async Task<IEnumerable<Patient>> GetAllAsync()
            => await _context.Patients
                             .AsNoTracking()
                             .Where(p => p.IsActivo)
                             .ToListAsync();

        /// <summary>Consulta optimizada: solo trae pacientes con IsActivo = true.</summary>
        public async Task<IEnumerable<Patient>> GetActivosAsync()
            => await _context.Patients
                             .AsNoTracking()
                             .Where(p => p.IsActivo)
                             .ToListAsync();

        /// <summary>
        /// Proyección mínima para dropdowns: trae solo Id, Nombre, Apellido y DNI.
        /// Evita cargar ObraSocial, FechaNacimiento y demás campos innecesarios.
        /// </summary>
        public async Task<IEnumerable<PatientSelectDto>> GetForSelectAsync()
            => await _context.Patients
                             .AsNoTracking()
                             .Where(p => p.IsActivo)
                             .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre)
                             .Select(p => new PatientSelectDto(p.Id, p.Nombre, p.Apellido, p.DNI))
                             .ToListAsync();

        /// <summary>
        /// Verifica si ya existe un paciente con el DNI dado.
        /// excludeId permite ignorar al propio paciente al editar (evita falso positivo).
        /// </summary>
        public async Task<bool> ExistsByDniAsync(string dni, int? excludeId = null)
            => await _context.Patients
                             .AsNoTracking()
                             .AnyAsync(p => p.DNI == dni && p.Id != excludeId);

        public async Task<(IEnumerable<Patient> Patients, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            var query = _context.Patients.AsNoTracking().Where(p => p.IsActivo).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    (p.Nombre + " " + p.Apellido).Contains(term) ||
                    p.DNI.Contains(term) ||
                    p.ObraSocial.Contains(term));
            }

            query = query.OrderBy(p => p.Apellido).ThenBy(p => p.Nombre);

            int totalCount = await query.CountAsync();
            var patients = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (patients, totalCount);
        }

        public async Task<int> CountActiveAsync()
            => await _context.Patients.AsNoTracking().CountAsync(p => p.IsActivo);

        public async Task<Patient> AddAsync(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task<Patient> UpdateAsync(Patient patient)
        {
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task DeleteAsync(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient is not null && patient.IsActivo)
            {
                patient.IsActivo = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}

