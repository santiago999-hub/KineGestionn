using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class BillingBatchTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int RequestedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public decimal EffectivenessPct => RequestedCount > 0
            ? Math.Round((decimal)UpdatedCount * 100m / RequestedCount, 2)
            : 0m;
    }

    public class BillingDashboardViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal DefaultSessionAmount { get; set; }
        public bool OnlyCompletedPending { get; set; }
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public int CompletedPendingCount { get; set; }
        public int Aging0To2DaysCount { get; set; }
        public int Aging3To7DaysCount { get; set; }
        public int Aging8PlusDaysCount { get; set; }
        public decimal PendingAmount => PendingCount * DefaultSessionAmount;
        public decimal PaidAmount => PaidCount * DefaultSessionAmount;
        public decimal CompletedPendingAmount => CompletedPendingCount * DefaultSessionAmount;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? Search { get; set; }
        public int? LastBatchRequestedCount { get; set; }
        public int? LastBatchUpdatedCount { get; set; }
        public int? LastBatchSkippedCount { get; set; }
        public decimal? LastBatchEffectivenessPct
            => LastBatchRequestedCount.HasValue && LastBatchRequestedCount.Value > 0 && LastBatchUpdatedCount.HasValue
                ? Math.Round((decimal)LastBatchUpdatedCount.Value * 100m / LastBatchRequestedCount.Value, 2)
                : null;
        public DateTime WeeklyBatchFrom { get; set; }
        public DateTime WeeklyBatchTo { get; set; }
        public int WeeklyBatchRuns { get; set; }
        public int WeeklyBatchRequestedCount { get; set; }
        public int WeeklyBatchUpdatedCount { get; set; }
        public int WeeklyBatchSkippedCount { get; set; }
        public decimal WeeklyBatchWarnThresholdPct { get; set; } = 70m;
        public decimal? WeeklyBatchEffectivenessPct
            => WeeklyBatchRequestedCount > 0
                ? Math.Round((decimal)WeeklyBatchUpdatedCount * 100m / WeeklyBatchRequestedCount, 2)
                : null;
        public bool IsWeeklyBatchEffectivenessLow
            => WeeklyBatchRuns > 0
               && WeeklyBatchEffectivenessPct.HasValue
               && WeeklyBatchEffectivenessPct.Value < WeeklyBatchWarnThresholdPct;
        public bool HasTwoConsecutiveLowWeeks { get; set; }
        public List<BillingBatchTrendPointViewModel> WeeklyTrendPoints { get; set; } = new();
        public List<SessionViewModel> Items { get; set; } = new();
    }
}
