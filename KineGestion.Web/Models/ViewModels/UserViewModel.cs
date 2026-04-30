using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Models.ViewModels
{
    public class UserViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        [StringLength(150, ErrorMessage = "El email no puede superar los 150 caracteres.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña")]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un rol.")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = string.Empty;

        /// <summary>
        /// Solo aplica cuando Rol == "Kinesiologo". Vincula el usuario a un Professional del sistema.
        /// Se guarda como Identity Claim "ProfessionalId".
        /// </summary>
        [Display(Name = "Profesional asociado")]
        public int? ProfessionalId { get; set; }

        public IEnumerable<SelectListItem> Roles { get; set; } = new List<SelectListItem>();

        /// <summary>Lista de profesionales activos para el selector (solo visible en rol Kinesiologo).</summary>
        public IEnumerable<SelectListItem> Profesionales { get; set; } = new List<SelectListItem>();
    }

    public class UserIndexViewModel
    {
        public IEnumerable<UserListItemViewModel> Items { get; set; } = new List<UserListItemViewModel>();
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;
    }

    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
    }
}
