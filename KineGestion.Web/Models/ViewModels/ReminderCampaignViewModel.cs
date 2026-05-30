using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class ReminderCampaignViewModel
    {
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public List<ReminderItemViewModel> Items { get; set; } = new();
    }

    public class ReminderItemViewModel
    {
        public int SessionId { get; set; }
        public DateTime FechaHora { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string? PacienteEmail { get; set; }
        public string? PacienteTelefono { get; set; }
        public string ProfesionalNombre { get; set; } = string.Empty;
        public string? TratamientoDescripcion { get; set; }
        public string ConfirmUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class ReminderResponseViewModel
    {
        public bool Success { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
