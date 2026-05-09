using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _context;

        public AuditLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(string? entityName, string? entityId, string? changedBy, int page, int pageSize)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (!string.IsNullOrWhiteSpace(entityId))
                query = query.Where(a => a.EntityId == entityId);

            if (!string.IsNullOrWhiteSpace(changedBy))
                query = query.Where(a => a.ChangedBy.Contains(changedBy));

            query = query.OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }
    }
}