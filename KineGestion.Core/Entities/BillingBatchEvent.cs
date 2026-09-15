using System;

namespace KineGestion.Core.Entities
{
    public class BillingBatchEvent
    {
        public int Id { get; set; }
        public string Operation { get; set; } = string.Empty;
        public int RequestedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }
        public string? FilterSearch { get; set; }
        public bool? OnlyCompletedPending { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}