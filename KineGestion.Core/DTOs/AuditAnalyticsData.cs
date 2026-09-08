using System;
using System.Collections.Generic;

namespace KineGestion.Core.DTOs
{
    public sealed class AuditAnalyticsData
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int TotalCount { get; set; }
        public IReadOnlyList<AuditMetricItem> ByAction { get; set; } = Array.Empty<AuditMetricItem>();
        public IReadOnlyList<AuditMetricItem> ByEntity { get; set; } = Array.Empty<AuditMetricItem>();
        public IReadOnlyList<AuditMetricItem> ByUser { get; set; } = Array.Empty<AuditMetricItem>();
        public IReadOnlyList<AuditDailyPoint> DailyTrend { get; set; } = Array.Empty<AuditDailyPoint>();
    }

    public sealed class AuditMetricItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class AuditDailyPoint
    {
        public DateTime DateUtc { get; set; }
        public int Count { get; set; }
    }
}