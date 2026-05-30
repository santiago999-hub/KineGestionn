using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class BillingDashboardViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal DefaultSessionAmount { get; set; }
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public decimal PendingAmount => PendingCount * DefaultSessionAmount;
        public decimal PaidAmount => PaidCount * DefaultSessionAmount;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? Search { get; set; }
        public List<SessionViewModel> Items { get; set; } = new();
    }
}
