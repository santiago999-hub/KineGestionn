using KineGestion.Core.DTOs;

namespace KineGestion.Web.Models.ViewModels
{
    public class ReminderFunnelDashboardViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateToExclusive { get; set; }
        public ReminderFunnelDto Funnel { get; set; } = new ReminderFunnelDto(0, 0, 0);
    }
}