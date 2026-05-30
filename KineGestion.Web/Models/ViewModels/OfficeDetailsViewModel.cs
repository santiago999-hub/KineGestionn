using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class OfficeDetailsViewModel
    {
        public OfficeViewModel Office { get; set; } = new OfficeViewModel();
        public IReadOnlyList<string> Professionals { get; set; } = new List<string>();
        public IReadOnlyList<string> Treatments { get; set; } = new List<string>();
        public IReadOnlyList<string> Equipments { get; set; } = new List<string>();
        public IReadOnlyList<string> ObrasSociales { get; set; } = new List<string>();
    }
}