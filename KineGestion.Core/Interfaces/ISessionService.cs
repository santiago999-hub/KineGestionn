using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionService
    {
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetAllForAdminAsync();
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        Task<IEnumerable<Session>> GetAllAsync();
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<int> CountAsync();
        Task<Session> CreateAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
