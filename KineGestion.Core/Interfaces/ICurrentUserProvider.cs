namespace KineGestion.Core.Interfaces
{
    public interface ICurrentUserProvider
    {
        string GetAuditIdentifier();
        bool IsInRole(string role);
        string? GetClaimValue(string claimType);
    }
}