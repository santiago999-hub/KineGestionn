using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class PatientDetailsViewModel
    {
        public PatientViewModel Patient { get; set; } = new PatientViewModel();
        public IEnumerable<TreatmentViewModel> Treatments { get; set; } = new List<TreatmentViewModel>();
        public IEnumerable<SessionViewModel> Sessions { get; set; } = new List<SessionViewModel>();
    }
}
