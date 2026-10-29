using System;
using System.Collections.Generic;
using KineGestion.Core;

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

    public class BillingDailyPointViewModel
    {
        public DateTime Day { get; set; }
        public string Label { get; set; } = string.Empty;
        public int PaidCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal CollectedAmount { get; set; }
    }

    public class BillingProfessionalViewModel
    {
        public string ProfessionalName { get; set; } = string.Empty;
        public int Completed { get; set; }
        public int CompletedPending { get; set; }
        public int CompletedPaid { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal CollectionPct => Completed > 0
            ? Math.Round((decimal)CompletedPaid * 100m / Completed, 2)
            : 0m;
    }

    public class BillingTopDebtViewModel
    {
        public string PatientLabel { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public int PendingCount { get; set; }
        public decimal PendingAmount { get; set; }
        public int AgeDays { get; set; }
    }

    public class BillingDashboardViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal DefaultSessionAmount { get; set; }
        public bool OnlyCompletedPending { get; set; }
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public int CompletedPaidCount { get; set; }
        public int CompletedPendingCount { get; set; }
        public int Aging0To2DaysCount { get; set; }
        public int Aging3To7DaysCount { get; set; }
        public int Aging8PlusDaysCount { get; set; }
        public decimal PendingAmount => PendingCount * DefaultSessionAmount;
        public decimal PaidAmount => PaidCount * DefaultSessionAmount;
        public decimal CompletedPendingAmount => CompletedPendingCount * DefaultSessionAmount;
        public decimal PreviousPaidAmount { get; set; }
        public decimal CollectionEffectivenessPct
        {
            get
            {
                var completed = CompletedPaidCount + CompletedPendingCount;
                return completed > 0
                    ? Math.Round((decimal)CompletedPaidCount * 100m / completed, 2)
                    : 0m;
            }
        }
        public decimal? CollectedVariationPct
        {
            get
            {
                if (PreviousPaidAmount <= 0)
                    return null;
                return Math.Round((PaidAmount - PreviousPaidAmount) * 100m / PreviousPaidAmount, 1);
            }
        }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0
            ? Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize))
            : 1;
        public string? Search { get; set; }
        public SessionStatus? StatusFilter { get; set; }
        public PaymentStatus? PaymentFilter { get; set; }
        public bool FiltersApplied { get; set; }
        public int RangeDays => Math.Max(1, (DateTo.Date - DateFrom.Date).Days + 1);
        public List<BillingDailyPointViewModel> DailyPoints { get; set; } = new();
        public List<BillingProfessionalViewModel> ProfessionalEffectiveness { get; set; } = new();
        public List<BillingTopDebtViewModel> TopPendingPatients { get; set; } = new();
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
