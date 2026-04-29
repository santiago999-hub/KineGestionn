using System.Collections.Generic;
using KineGestion.Core;

namespace KineGestion.Web.Models.ViewModels
{
    public class SessionIndexViewModel
    {
        public IEnumerable<SessionViewModel> Items { get; set; } = new List<SessionViewModel>();

        public string? Search { get; set; }
        public SessionStatus? Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public string SortBy { get; set; } = "fecha";
        public string SortDir { get; set; } = "desc";

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public int TotalPages => TotalCount <= 0 ? 1 : (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}
