using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionService
    {
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetAllForAdminAsync();
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus);
        Task<IEnumerable<Session>> GetAllAsync();
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<int> CountAsync();
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<Session> CreateAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
