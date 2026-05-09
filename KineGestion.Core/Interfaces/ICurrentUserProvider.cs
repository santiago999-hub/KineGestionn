namespace KineGestion.Core.Interfaces
{
    public interface ICurrentUserProvider
    {
        string GetAuditIdentifier();
    }
}