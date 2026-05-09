using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index(string? entityName, string? entityId, string? changedBy, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (items, totalCount) = await _auditLogService.GetPagedAsync(entityName, entityId, changedBy, page, pageSize);

            var model = new AuditIndexViewModel
            {
                Items = items.Select(AuditLogViewModel.FromEntity).ToList(),
                EntityName = entityName,
                EntityId = entityId,
                ChangedBy = changedBy,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }
    }
}