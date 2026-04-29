using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class OfficeIndexViewModel
    {
        public IEnumerable<OfficeViewModel> Items { get; set; } = new List<OfficeViewModel>();

        public string? Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public int TotalPages => TotalCount <= 0 ? 1 : (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}
