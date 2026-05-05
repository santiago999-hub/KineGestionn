using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
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

        /// <summary>
        /// Carga la sesión con sus relaciones para visualización o edición.
        /// AsNoTracking: no registra la entidad en el ChangeTracker, reduciendo overhead de memoria.
        /// El UpdateAsync adjunta el objeto modificado explícitamente, por lo que no se necesita tracking aquí.
        /// </summary>
        public async Task<Session?> GetByIdAsync(int id)
            => await _context.Sessions
                             .AsNoTracking()
                             .Include(s => s.Patient)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .Include(s => s.Office)
                             .FirstOrDefaultAsync(s => s.Id == id);

        /// <summary>OBSOLETO: carga la tabla completa en memoria. Ver interfaz para detalles del riesgo.</summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync.")]
        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _context.Sessions
                             .AsNoTracking()
                             .Include(s => s.Patient)
                             .Include(s => s.Professional)
                             .Include(s => s.Treatment)
                             .Include(s => s.Office)
                             .OrderByDescending(s => s.FechaHora)
                             .ToListAsync();

        /// <summary>OBSOLETO: usa 4 Includes completos. Ver interfaz. Usar GetPagedListForAdminAsync.</summary>
        [Obsolete("Carga entidades completas con 4 JOINs. Usar GetPagedListForAdminAsync.")]
        public async Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            string? sortBy,
            string? sortDir)
        {
            var query = _context.Sessions
                .AsNoTracking()
                .Include(s => s.Patient)
                .Include(s => s.Professional)
                .Include(s => s.Treatment)
                .Include(s => s.Office)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    (s.Patient != null && (s.Patient.Nombre + " " + s.Patient.Apellido).Contains(term)) ||
                    (s.Professional != null && (s.Professional.Nombre + " " + s.Professional.Apellido).Contains(term)) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);

            if (paymentStatus.HasValue)
                query = query.Where(s => s.PaymentStatus == paymentStatus.Value);

            var sortField = string.IsNullOrWhiteSpace(sortBy) ? "fecha" : sortBy.Trim().ToLowerInvariant();
            var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

            query = (sortField, descending) switch
            {
                ("estado", true) => query.OrderByDescending(s => s.Status).ThenByDescending(s => s.FechaHora),
                ("estado", false) => query.OrderBy(s => s.Status).ThenByDescending(s => s.FechaHora),
                (_, true) => query.OrderByDescending(s => s.FechaHora),
                _ => query.OrderBy(s => s.FechaHora)
            };

            int totalCount = await query.CountAsync();

            var sessions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (sessions, totalCount);
        }

        /// <summary>
        /// Proyección SQL directa: solo trae los campos necesarios para la tabla admin.
        /// Evita cargar nav properties completas de Patient, Professional, Treatment y Office.
        /// </summary>
        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            string? sortBy,
            string? sortDir)
        {
            var query = _context.Sessions.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    (s.Patient != null && (s.Patient.Nombre + " " + s.Patient.Apellido).Contains(term)) ||
                    (s.Professional != null && (s.Professional.Nombre + " " + s.Professional.Apellido).Contains(term)) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)       query = query.Where(s => s.Status == status.Value);
            if (paymentStatus.HasValue) query = query.Where(s => s.PaymentStatus == paymentStatus.Value);

            var sortField = string.IsNullOrWhiteSpace(sortBy) ? "fecha" : sortBy.Trim().ToLowerInvariant();
            var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

            query = (sortField, descending) switch
            {
                ("estado", true)  => query.OrderByDescending(s => s.Status).ThenByDescending(s => s.FechaHora),
                ("estado", false) => query.OrderBy(s => s.Status).ThenByDescending(s => s.FechaHora),
                (_, true)  => query.OrderByDescending(s => s.FechaHora),
                _ => query.OrderBy(s => s.FechaHora)
            };

            int totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SessionListDto(
                    s.Id,
                    s.FechaHora,
                    s.Status,
                    s.PaymentStatus,
                    s.NroSesionEnTratamiento,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : string.Empty,
                    s.Professional != null ? s.Professional.Apellido + ", " + s.Professional.Nombre : string.Empty,
                    s.Treatment != null ? s.Treatment.Descripcion : null,
                    s.Office != null ? s.Office.Name : null,
                    s.EvolutionLockedAt.HasValue
                ))
                .ToListAsync();

            return (items, totalCount);
        }

        [Obsolete("Carga entidades completas con 3 JOINs. Usar GetPagedListByProfessionalAsync.")]
        public async Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedByProfessionalAsync(
            int professionalId,
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus)
        {
            var query = _context.Sessions
                .AsNoTracking()
                .Include(s => s.Patient)
                .Include(s => s.Treatment)
                .Include(s => s.Office)
                .Where(s => s.ProfessionalId == professionalId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    (s.Patient != null && (s.Patient.Nombre + " " + s.Patient.Apellido).Contains(term)) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);

            if (paymentStatus.HasValue)
                query = query.Where(s => s.PaymentStatus == paymentStatus.Value);

            int totalCount = await query.CountAsync();

            var sessions = await query
                .OrderByDescending(s => s.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (sessions, totalCount);
        }

        /// <summary>
        /// Proyección SQL directa para la agenda del kinesiológo.
        /// No carga nav properties de Professional (ya está filtrado por professionalId).
        /// </summary>
        public async Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(
            int professionalId,
            int page,
            int pageSize,
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus)
        {
            var query = _context.Sessions
                .AsNoTracking()
                .Where(s => s.ProfessionalId == professionalId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    (s.Patient != null && (s.Patient.Nombre + " " + s.Patient.Apellido).Contains(term)) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)       query = query.Where(s => s.Status == status.Value);
            if (paymentStatus.HasValue) query = query.Where(s => s.PaymentStatus == paymentStatus.Value);

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SessionListDto(
                    s.Id,
                    s.FechaHora,
                    s.Status,
                    s.PaymentStatus,
                    s.NroSesionEnTratamiento,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : string.Empty,
                    string.Empty,   // el profesional ya es el usuario actual
                    s.Treatment != null ? s.Treatment.Descripcion : null,
                    s.Office != null ? s.Office.Name : null,
                    s.EvolutionLockedAt.HasValue
                ))
                .ToListAsync();

            return (items, totalCount);
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

        public async Task<int> CountAsync()
            => await _context.Sessions.AsNoTracking().CountAsync();

        /// <summary>
        /// Inserta una sesión de forma atómica bajo una transacción RepeatableRead.
        /// Dentro de la transacción se recuenta las sesiones del tratamiento y se re-valida el límite,
        /// eliminando la condición de carrera (TOCTOU) que existía cuando el conteo y el INSERT
        /// ocurrían en operaciones de base de datos separadas.
        /// </summary>
        public async Task<Session> AddAsync(Session session)
        {
            // RepeatableRead: garantiza que ninguna otra transacción pueda insertar filas para
            // el mismo TreatmentId entre el COUNT y el INSERT de esta transacción.
            await using var tx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead);

            // Conteo atómico: este valor es la fuente de verdad bajo concurrencia.
            int sesionesActuales = await _context.Sessions
                .CountAsync(s => s.TreatmentId == session.TreatmentId);

            // Re-validación del límite del tratamiento dentro de la transacción.
            var treatment = await _context.Treatments
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == session.TreatmentId);

            if (treatment is not null && sesionesActuales >= treatment.CantidadSesionesTotales)
                throw new BusinessValidationException(
                    $"El tratamiento ya alcanzó el límite de {treatment.CantidadSesionesTotales} sesiones.",
                    nameof(Session.TreatmentId));

            // El número de sesión se calcula de forma atómica: es el valor correcto incluso bajo concurrencia.
            session.NroSesionEnTratamiento = sesionesActuales + 1;

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

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
