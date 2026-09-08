using System.Collections.Generic;
using KineGestion.Core.Entities;

namespace KineGestion.Web.Models.ViewModels
{
    public class DispatchQueueIndexViewModel
    {
        public DispatchQueueStats Stats { get; set; } = new();
        public IReadOnlyList<DispatchJob> Jobs { get; set; } = System.Array.Empty<DispatchJob>();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public DispatchJobStatus? FilterStatus { get; set; }
        public string? FilterDispatchType { get; set; }
        public string? Search { get; set; }
        public IReadOnlyList<string> DispatchTypes { get; set; } = System.Array.Empty<string>();
    }
}