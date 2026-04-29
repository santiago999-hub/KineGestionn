using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using KineGestion.Core;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionRepository
    {
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetAllAsync();
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<IEnumerable<Session>> GetByTreatmentIdAsync(int treatmentId);
        Task<bool> ExistsProfessionalConflictAsync(int professionalId, DateTime fechaHora, int windowInMinutes = 45, int? excludeSessionId = null);
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<int> CountAsync();
        Task<Session> AddAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
