namespace KineGestion.Web.Models.ViewModels
{
    public class BillingOperationalAlertHistoryItemViewModel
    {
        public DateTime ChangedAtUtc { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public bool IsSystemTriggered => string.Equals(ChangedBy, "system", StringComparison.OrdinalIgnoreCase);
        public string TriggerSourceLabel => IsSystemTriggered ? "System" : "Manual";
    }

    public class HomeDashboardViewModel
    {
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
        public bool IsBillingOperationalAlertActive { get; set; }
        public bool IsBillingOperationalAlertSentToday { get; set; }
        public DateTime? LastBillingOperationalAlertAtUtc { get; set; }
        public string? LastBillingOperationalAlertChangedBy { get; set; }
        public List<BillingOperationalAlertHistoryItemViewModel> RecentBillingOperationalAlerts { get; set; } = new();
    }
}
