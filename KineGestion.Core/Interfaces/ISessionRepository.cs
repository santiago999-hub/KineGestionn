using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using KineGestion.Core;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// CRUD y consultas de integridad del agregado Session.
    /// Los listados paginados, métricas/KPIs y operaciones batch viven en interfaces
    /// especializadas (ISessionQueryRepository, ISessionMetricsRepository, ISessionBatchRepository)
    /// para que cada consumidor dependa solo de lo que usa.
    /// </summary>
    public interface ISessionRepository
    {
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<IEnumerable<Session>> GetByTreatmentIdAsync(int treatmentId);
        Task<bool> ExistsProfessionalConflictAsync(int professionalId, DateTime fechaHora, int windowInMinutes = 45, int? excludeSessionId = null);
        Task<IReadOnlyList<DateTime>> GetProfessionalBusyTimesAsync(int professionalId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc);
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<Session> AddAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}