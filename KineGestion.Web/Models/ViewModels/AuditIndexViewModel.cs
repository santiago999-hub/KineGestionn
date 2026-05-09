using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class AuditIndexViewModel
    {
        public IEnumerable<AuditLogViewModel> Items { get; set; } = new List<AuditLogViewModel>();
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public string? ChangedBy { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount <= 0 ? 1 : (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}