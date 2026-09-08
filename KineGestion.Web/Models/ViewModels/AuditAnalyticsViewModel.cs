using System;
using KineGestion.Core.DTOs;

namespace KineGestion.Web.Models.ViewModels
{
    public class AuditAnalyticsViewModel
    {
        public AuditAnalyticsData Data { get; set; } = new();

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int MaxByAction { get; set; }
        public int MaxByEntity { get; set; }
        public int MaxByUser { get; set; }
        public int MaxTrend { get; set; }
    }
}