namespace KineGestion.Core
{
    public enum AuditEntityType
    {
        Patient = 0,
        Professional = 1,
        Treatment = 2,
        Session = 3,
        Office = 4,
        Equipment = 5,
        BillingBatch = 6
    }

    public enum AuditActionType
    {
        Create = 0,
        Update = 1,
        Delete = 2
    }

    public enum SessionStatus
    {
        Pending = 0,
        Completed = 1,
        Canceled = 2
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1
    }
}