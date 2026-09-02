using System.Security.Claims;
using KineGestion.Core.Interfaces;

namespace KineGestion.Web.Services;

public class HttpContextCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetAuditIdentifier()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "system";

        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }

    public bool IsInRole(string role)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.IsInRole(role) == true;
    }

    public string? GetClaimValue(string claimType)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirstValue(claimType);
    }
}