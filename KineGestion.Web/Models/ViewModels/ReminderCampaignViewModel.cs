using System;
using System.Collections.Generic;
using System.Linq;

namespace KineGestion.Web.Models.ViewModels
{
    public class ReminderCampaignViewModel
    {
        public int HoursAhead { get; set; }
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public List<int> OperationalWindowsHours { get; set; } = new();
        public string OperationalWindowsLabel => OperationalWindowsHours.Count == 0
            ? "24h + 3h"
            : string.Join(" + ", OperationalWindowsHours.Select(w => $"{w}h"));
        public int OperationalCandidatesCount { get; set; }
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

    public class ReminderTestResultViewModel
    {
        public bool DryRun { get; set; }
        public int SessionId { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string? DestinoEmail { get; set; }
        public string? DestinoWhatsApp { get; set; }
        public string EmailSubject { get; set; } = string.Empty;
        public string EmailBody { get; set; } = string.Empty;
        public string WhatsAppBody { get; set; } = string.Empty;
        public bool CanEmail { get; set; }
        public bool CanWhatsApp { get; set; }
        public bool EmailSent { get; set; }
        public bool WhatsAppSent { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
