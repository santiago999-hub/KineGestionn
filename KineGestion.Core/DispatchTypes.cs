namespace KineGestion.Core
{
    public static class DispatchTypes
    {
        public const string PatientReminder = "PatientReminder";
        public const string BillingFollowUpPrefix = "BillingFollowUp:";
        public const string BillingBatchLowEffectivenessAlert = "BillingBatchLowEffectivenessAlert";

        public static string BillingFollowUp(string tier)
            => BillingFollowUpPrefix + tier;
    }
}