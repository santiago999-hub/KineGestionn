using System.ComponentModel.DataAnnotations;
using KineGestion.Core.Entities;

namespace KineGestion.Web.Models.ViewModels
{
    public class OfficeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del consultorio es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;

        public static OfficeViewModel FromEntity(Office office) => new()
        {
            Id = office.Id,
            Name = office.Name,
            IsActive = office.IsActive
        };

        public Office ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            IsActive = IsActive
        };
    }
}
