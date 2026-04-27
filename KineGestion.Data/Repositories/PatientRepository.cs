using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        /// Verifica si ya existe un paciente con el DNI dado.
        /// excludeId permite ignorar al propio paciente al editar (evita falso positivo).
        /// </summary>
        public async Task<bool> ExistsByDniAsync(string dni, int? excludeId = null)
            => await _context.Patients
                             .AsNoTracking()
                             .AnyAsync(p => p.DNI == dni && p.Id != excludeId);

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

