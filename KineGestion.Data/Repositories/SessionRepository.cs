using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using System.Linq;

namespace KineGestion.Data.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly AppDbContext _context;

        public SessionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Session?> GetByIdAsync(int id)
            => await _context.Sessions
                             .Include(s => s.Patient)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _context.Sessions
                             .AsNoTracking()
                             .Include(s => s.Patient)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .OrderByDescending(s => s.FechaHora)
                             .ToListAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _context.Sessions
                             .AsNoTracking()
                             .Where(s => s.PatientId == patientId)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .OrderBy(s => s.NroSesionEnTratamiento)
                             .ToListAsync();

        public async Task<IEnumerable<Session>> GetByTreatmentIdAsync(int treatmentId)
            => await _context.Sessions
                             .AsNoTracking()
                             .Where(s => s.TreatmentId == treatmentId)
                             .OrderBy(s => s.NroSesionEnTratamiento)
                             .ToListAsync();

        public async Task<bool> ExistsProfessionalConflictAsync(int professionalId, DateTime fechaHora, int windowInMinutes = 45, int? excludeSessionId = null)
        {
            var minFecha = fechaHora.AddMinutes(-windowInMinutes);
            var maxFecha = fechaHora.AddMinutes(windowInMinutes);

            return await _context.Sessions
                                 .AsNoTracking()
                                 .AnyAsync(s => s.ProfessionalId == professionalId
                                             && s.Id != excludeSessionId
                                             && s.FechaHora >= minFecha
                                             && s.FechaHora <= maxFecha);
        }

        public async Task<Session> AddAsync(Session session)
        {
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<Session> UpdateAsync(Session session)
        {
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task DeleteAsync(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session is not null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
            }
        }
    }
}
