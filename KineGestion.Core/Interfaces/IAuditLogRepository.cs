using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page,
            int pageSize);

        Task<IEnumerable<AuditLog>> GetAllAsync(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo);
    }
}
