using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(string? entityName, string? entityId, string? changedBy, int page, int pageSize);
    }
}