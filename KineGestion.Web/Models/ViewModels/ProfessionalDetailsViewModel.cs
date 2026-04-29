using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class ProfessionalDetailsViewModel
    {
        public ProfessionalViewModel Professional { get; set; } = new ProfessionalViewModel();
        public IEnumerable<SessionViewModel> Sessions { get; set; } = new List<SessionViewModel>();
    }
}
