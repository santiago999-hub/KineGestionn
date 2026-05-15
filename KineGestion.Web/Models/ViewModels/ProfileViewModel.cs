using System;
using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class ProfileViewModel
    {
        public string DisplayName { get; set; } = "Usuario";
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    }
}
