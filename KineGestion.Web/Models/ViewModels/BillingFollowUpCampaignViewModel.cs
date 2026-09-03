using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class BillingFollowUpCampaignViewModel
    {
        public DateTime AsOfUtc { get; set; }
        public int MinAgeDays { get; set; } = 1;
        public int MaxAgeDays { get; set; } = 9999;
        public List<BillingFollowUpTierGroupViewModel> TierGroups { get; set; } = new();
        public int TotalCandidates { get; set; }
        public List<BillingFollowUpDispatchHistoryItemViewModel> History { get; set; } = new();
    }

    public class BillingFollowUpTierGroupViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int MinAgeDays { get; set; }
        public int MaxAgeDays { get; set; }
        public List<BillingFollowUpItemViewModel> Items { get; set; } = new();
    }

    public class BillingFollowUpItemViewModel
    {
        public int SessionId { get; set; }
        public DateTime FechaHora { get; set; }
        public int PaymentAgeDays { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string? PacienteEmail { get; set; }
        public string? PacienteTelefono { get; set; }
        public string ProfesionalNombre { get; set; } = string.Empty;
        public string? TratamientoDescripcion { get; set; }
        public string? EmailSubject { get; set; }
        public string? EmailBody { get; set; }
        public string? WhatsAppBody { get; set; }
        public bool CanEmail => !string.IsNullOrWhiteSpace(PacienteEmail);
        public bool CanWhatsApp => !string.IsNullOrWhiteSpace(PacienteTelefono);
    }

    public class BillingFollowUpDispatchHistoryItemViewModel
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public int SessionId { get; set; }
        public string ChannelSummary { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorSummary { get; set; }
    }

    public class BillingFollowUpTestResultViewModel
    {
        public bool DryRun { get; set; }
        public int SessionId { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string TierLabel { get; set; } = string.Empty;
        public int PaymentAgeDays { get; set; }
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
