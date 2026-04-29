using Microsoft.AspNetCore.Identity;

namespace KineGestion.Web.Models
{
    /// <summary>
    /// Usuario del sistema. Extiende IdentityUser para permitir propiedades adicionales en el futuro.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        public string NombreCompleto { get; set; } = string.Empty;
    }
}
