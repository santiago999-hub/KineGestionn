using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _repository;

        public AuditLogService(IAuditLogRepository repository)
        {
            _repository = repository;
        }

        public Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(string? entityName, string? entityId, string? changedBy, int page, int pageSize)
            => _repository.GetPagedAsync(entityName, entityId, changedBy, page, pageSize);
    }
}