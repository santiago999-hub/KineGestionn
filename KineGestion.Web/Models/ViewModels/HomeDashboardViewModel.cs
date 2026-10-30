namespace KineGestion.Web.Models.ViewModels
{
    public class CancellationReasonCountViewModel
    {
        public Core.CancellationReason Reason { get; set; }
        public int Count { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class BillingOperationalAlertHistoryItemViewModel
    {
        public DateTime ChangedAtUtc { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public bool IsSystemTriggered => string.Equals(ChangedBy, "system", StringComparison.OrdinalIgnoreCase);
        public string TriggerSourceLabel => IsSystemTriggered ? "System" : "Manual";
    }

    public class AgendaItemViewModel
    {
        public int SessionId { get; set; }
        public DateTime FechaHora { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string ProfesionalNombre { get; set; } = string.Empty;
        public string? OfficeNombre { get; set; }
        public Core.SessionStatus Status { get; set; }
        public Core.PaymentStatus PaymentStatus { get; set; }
    }

    public class DailySessionsPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Canceled { get; set; }
    }

    public class StatusDistributionItemViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Tone { get; set; } = string.Empty;
    }

    public class HomeDashboardViewModel
    {
        public int? SelectedOfficeId { get; set; }
        public string? OfficeName { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public int PacientesActivosCount { get; set; }
        public int ProfesionalesActivosCount { get; set; }
        public int TratamientosCount { get; set; }
        public int SesionesCount { get; set; }
        public int SesionesHoyCount { get; set; }
        public int SesionesCompletadasHoyCount { get; set; }
        public int SesionesCanceladasHoyCount { get; set; }
        public int SesionesPendientesPagoCount { get; set; }
        public int SesionesPendientesConfirmacionCount { get; set; }
        public decimal CompletionRateToday { get; set; }
        public decimal CollectionRateLast30Days { get; set; }
        public decimal CancellationRateLast30Days { get; set; }
        public int LateCancellationsLast30 { get; set; }
        public decimal LateCancellationRateLast30 { get; set; }
        public List<CancellationReasonCountViewModel> TopCancellationReasons { get; set; } = new();
        public List<AgendaItemViewModel> TodayAgenda { get; set; } = new();
        public List<DailySessionsPointViewModel> DailySessions { get; set; } = new();
        public List<StatusDistributionItemViewModel> StatusDistribution { get; set; } = new();
        public bool IsBillingOperationalAlertActive { get; set; }
        public bool IsBillingOperationalAlertSentToday { get; set; }
        public DateTime? LastBillingOperationalAlertAtUtc { get; set; }
        public string? LastBillingOperationalAlertChangedBy { get; set; }
        public List<BillingOperationalAlertHistoryItemViewModel> RecentBillingOperationalAlerts { get; set; } = new();
    }
}
