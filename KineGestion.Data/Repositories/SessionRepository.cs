using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System;
using Microsoft.Data.SqlClient;
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
            DateTime? dateFrom,
            DateTime? dateTo,
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

            if (dateFrom.HasValue)
                query = query.Where(s => s.FechaHora >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                query = query.Where(s => s.FechaHora < dateTo.Value.Date.AddDays(1));

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
            DateTime? dateFrom,
            DateTime? dateTo,
            string? sortBy,
            string? sortDir)
        {
            var baseQuery = _context.Sessions.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(s =>
                    (s.Patient != null && (
                        s.Patient.Nombre.Contains(term) ||
                        s.Patient.Apellido.Contains(term) ||
                        s.Patient.DNI.Contains(term))) ||
                    (s.Professional != null && (
                        s.Professional.Nombre.Contains(term) ||
                        s.Professional.Apellido.Contains(term) ||
                        s.Professional.Matricula.Contains(term))) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)       baseQuery = baseQuery.Where(s => s.Status == status.Value);
            if (paymentStatus.HasValue) baseQuery = baseQuery.Where(s => s.PaymentStatus == paymentStatus.Value);
            if (dateFrom.HasValue) baseQuery = baseQuery.Where(s => s.FechaHora >= dateFrom.Value.Date);
            if (dateTo.HasValue) baseQuery = baseQuery.Where(s => s.FechaHora < dateTo.Value.Date.AddDays(1));

            int totalCount = await baseQuery.CountAsync();

            var sortField = string.IsNullOrWhiteSpace(sortBy) ? "fecha" : sortBy.Trim().ToLowerInvariant();
            var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

            var sortedQuery = (sortField, descending) switch
            {
                ("estado", true)  => baseQuery.OrderByDescending(s => s.Status).ThenByDescending(s => s.FechaHora),
                ("estado", false) => baseQuery.OrderBy(s => s.Status).ThenByDescending(s => s.FechaHora),
                (_, true)  => baseQuery.OrderByDescending(s => s.FechaHora),
                _ => baseQuery.OrderBy(s => s.FechaHora)
            };

            var items = await sortedQuery
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
            PaymentStatus? paymentStatus,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var baseQuery = _context.Sessions
                .AsNoTracking()
                .Where(s => s.ProfessionalId == professionalId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                baseQuery = baseQuery.Where(s =>
                    (s.Patient != null && (
                        s.Patient.Nombre.Contains(term) ||
                        s.Patient.Apellido.Contains(term) ||
                        s.Patient.DNI.Contains(term))) ||
                    (s.Treatment != null && s.Treatment.Descripcion.Contains(term)));
            }

            if (status.HasValue)       baseQuery = baseQuery.Where(s => s.Status == status.Value);
            if (paymentStatus.HasValue) baseQuery = baseQuery.Where(s => s.PaymentStatus == paymentStatus.Value);
            if (dateFrom.HasValue) baseQuery = baseQuery.Where(s => s.FechaHora >= dateFrom.Value.Date);
            if (dateTo.HasValue) baseQuery = baseQuery.Where(s => s.FechaHora < dateTo.Value.Date.AddDays(1));

            int totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
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

        public async Task<int> CountTodayAsync(DateTime utcToday)
        {
            var tomorrow = utcToday.Date.AddDays(1);
            return await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.FechaHora >= utcToday.Date && s.FechaHora < tomorrow);
        }

        public async Task<int> CountByPaymentStatusAsync(PaymentStatus paymentStatus)
            => await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.PaymentStatus == paymentStatus);

        public async Task<int> CountByStatusAsync(SessionStatus status)
            => await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.Status == status);

        public async Task<int> CountByStatusOnDateAsync(SessionStatus status, DateTime utcDay)
        {
            var tomorrow = utcDay.Date.AddDays(1);
            return await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.Status == status && s.FechaHora >= utcDay.Date && s.FechaHora < tomorrow);
        }

        public async Task<int> CountInRangeAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<int> CountByStatusInRangeAsync(SessionStatus status, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.Status == status && s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<int> CountByPaymentStatusInRangeAsync(PaymentStatus paymentStatus, DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.PaymentStatus == paymentStatus && s.FechaHora >= fromInclusiveUtc && s.FechaHora < toExclusiveUtc);

        public async Task<IEnumerable<SessionReminderCandidateDto>> GetReminderCandidatesAsync(DateTime fromInclusiveUtc, DateTime toExclusiveUtc)
            => await _context.Sessions
                .AsNoTracking()
                .Where(s => s.Status == SessionStatus.Pending
                    && s.FechaHora >= fromInclusiveUtc
                    && s.FechaHora < toExclusiveUtc)
                .OrderBy(s => s.FechaHora)
                .Select(s => new SessionReminderCandidateDto(
                    s.Id,
                    s.FechaHora,
                    s.Patient != null ? s.Patient.Apellido + ", " + s.Patient.Nombre : "Paciente",
                    s.Patient != null ? s.Patient.Email : null,
                    s.Patient != null ? s.Patient.Telefono : null,
                    s.Professional != null ? s.Professional.Apellido + ", " + s.Professional.Nombre : "Profesional",
                    s.Treatment != null ? s.Treatment.Descripcion : null
                ))
                .ToListAsync();

        /// <summary>
        /// Inserta una sesión de forma atómica bajo una transacción Serializable.
        /// Se recalcula el conteo dentro de la transacción para evitar phantoms entre el COUNT
        /// y el INSERT. Si SQL Server detecta un deadlock transitorio, se reintenta unas pocas
        /// veces antes de propagar el error.
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
                    int sesionesActuales = await _context.Sessions
                        .CountAsync(s => s.TreatmentId == session.TreatmentId);

                    var treatment = await _context.Treatments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == session.TreatmentId);

                    if (treatment is not null && sesionesActuales >= treatment.CantidadSesionesTotales)
                        throw new BusinessValidationException(
                            $"El tratamiento ya alcanzó el límite de {treatment.CantidadSesionesTotales} sesiones.",
                            nameof(Session.TreatmentId));

                    session.NroSesionEnTratamiento = sesionesActuales + 1;

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

        private static bool IsDeadlock(Exception ex)
            => ex is SqlException { Number: 1205 }
            || ex.InnerException is SqlException { Number: 1205 };

        private static bool IsUniqueConstraintViolation(Exception ex)
            => ex is SqlException { Number: 2601 or 2627 }
            || ex.InnerException is SqlException { Number: 2601 or 2627 }
            || ex.InnerException?.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
