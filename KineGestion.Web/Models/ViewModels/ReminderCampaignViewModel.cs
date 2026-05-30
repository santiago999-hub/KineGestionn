using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class ReminderCampaignViewModel
    {
        public int HoursAhead { get; set; }
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public List<ReminderItemViewModel> Items { get; set; } = new();
        public List<ReminderDispatchHistoryItemViewModel> History { get; set; } = new();
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
        public bool CanEmail => !string.IsNullOrWhiteSpace(PacienteEmail);
        public bool CanWhatsApp => !string.IsNullOrWhiteSpace(PacienteTelefono);
    }

    public class ReminderResponseViewModel
    {
        public bool Success { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ReminderDispatchHistoryItemViewModel
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public int SessionId { get; set; }
        public string ChannelSummary { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorSummary { get; set; }
    }
}
