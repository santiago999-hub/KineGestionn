using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using System.Linq;

namespace KineGestion.Data.Repositories
{
    /// <summary>
    /// CRUD del agregado Session y consultas de integridad/registro por entidad.
    /// Los listados paginados viven en <see cref="SessionQueryRepository"/>,
    /// los KPIs en <see cref="SessionMetricsRepository"/> y las operaciones
    /// batch en <see cref="SessionBatchRepository"/>.
    /// </summary>
    public class SessionRepository : SessionRepositoryBase, ISessionRepository
    {
        public SessionRepository(AppDbContext context) : base(context)
        {
        }

        public SessionRepository(AppDbContext context, ICurrentUserProvider currentUserProvider) : base(context, currentUserProvider)
        {
        }

        /// <summary>
        /// Carga la sesión con sus relaciones para visualización o edición.
        /// AsNoTracking: no registra la entidad en el ChangeTracker, reduciendo overhead de memoria.
        /// El UpdateAsync adjunta el objeto modificado explícitamente, por lo que no se necesita tracking aquí.
        /// Para Asistente: se ocultan campos clínicos sensibles (Evolution, InternalNotes).
        /// </summary>
        public async Task<Session?> GetByIdAsync(int id)
        {
            var session = await _context.Sessions
                             .AsNoTracking()
                             .Include(s => s.Patient)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .Include(s => s.Office)
                             .FirstOrDefaultAsync(s => s.Id == id);

            if (session is not null && !CanViewClinicalNotes())
            {
                session.Evolution = null;
                session.InternalNotes = null;
            }

            return session;
        }

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _context.Sessions
                             .AsNoTracking()
                             .Where(s => s.PatientId == patientId)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .Include(s => s.Office)
                             .OrderBy(s => s.NroSesionEnTratamiento)
                             .ToListAsync();

        /// <summary>
        /// Retorna las últimas sesiones de un profesional para la vista de detalle.
        /// El límite <see cref="MaxRecentSessionsInDetail"/> evita cargar historial ilimitado
        /// en un contexto donde solo se muestran sesiones recientes como resumen.
        /// Para listados completos con paginación, usar GetPagedListByProfessionalAsync.
        /// </summary>
        private const int MaxRecentSessionsInDetail = 20;

        public async Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId)
            => await _context.Sessions
                             .AsNoTracking()
                             .Where(s => s.ProfessionalId == professionalId)
                             .Include(s => s.Patient)
                             .Include(s => s.Treatment)
                             .Include(s => s.Office)
                             .OrderByDescending(s => s.FechaHora)
                             .Take(MaxRecentSessionsInDetail)
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

        public async Task<IReadOnlyList<DateTime>> GetProfessionalBusyTimesAsync(int professionalId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                             .AsNoTracking()
                             .Where(s => s.ProfessionalId == professionalId
                                 && s.Status != SessionStatus.Canceled
                                 && s.FechaHora >= fromInclusiveUtc
                                 && s.FechaHora < toExclusiveUtc)
                             .Select(s => s.FechaHora)
                             .ToListAsync();

        public async Task<int> CountByTreatmentIdAsync(int treatmentId)
            => await _context.Sessions
                             .AsNoTracking()
                             .CountAsync(s => s.TreatmentId == treatmentId);

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await _context.Sessions
                             .AsNoTracking()
                             .CountAsync(s => s.PatientId == patientId);

        public async Task<int> CountByProfessionalIdAsync(int professionalId)
            => await _context.Sessions
                             .AsNoTracking()
                             .CountAsync(s => s.ProfessionalId == professionalId);

        public async Task<int> CountByOfficeIdAsync(int officeId)
            => await _context.Sessions
                             .AsNoTracking()
                             .CountAsync(s => s.OfficeId == officeId);

        private static bool IsDeadlock(Exception ex)
            => ex is SqlException { Number: 1205 }
            || ex.InnerException is SqlException { Number: 1205 };

        private static bool IsUniqueConstraintViolation(Exception ex)
            => ex is SqlException { Number: 2601 or 2627 }
            || ex.InnerException is SqlException { Number: 2601 or 2627 }
            || ex.InnerException?.InnerException is SqlException { Number: 2601 or 2627 };

        /// <summary>
        /// Inserta una sesión de forma atómica bajo una transacción Serializable.
        /// La numeración (NroSesionEnTratamiento) y la validación del límite de sesiones
        /// del tratamiento son regla de negocio del SessionService (CreateAsync/ReprogramAsync):
        /// el repositorio solo se encarga de persistir. La integridad ante carreras se protege
        /// con el índice único (TreatmentId, NroSesionEnTratamiento): si dos recálculos chocan,
        /// se detecta la violación y se pide reintentar.
        /// </summary>
        public async Task<Session> AddAsync(Session session)
        {
            const int maxDeadlockRetries = 3;

            for (int attempt = 1; ; attempt++)
            {
                await using var tx = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    _context.Sessions.Add(session);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    return session;
                }
                catch (Exception ex) when (IsDeadlock(ex) && attempt < maxDeadlockRetries)
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                }
                catch (Exception ex) when (IsDeadlock(ex))
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw new BusinessValidationException(
                        "No se pudo guardar la sesión por concurrencia alta. Por favor, intentá nuevamente.",
                        nameof(Session.NroSesionEnTratamiento));
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw new BusinessValidationException(
                        "La numeración de sesión se actualizó por concurrencia. Reintentá guardar para asignar el próximo número disponible.",
                        nameof(Session.NroSesionEnTratamiento));
                }
            }
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