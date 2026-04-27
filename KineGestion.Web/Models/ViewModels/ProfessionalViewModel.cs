using System.ComponentModel.DataAnnotations;
using KineGestion.Core.Entities;

namespace KineGestion.Web.Models.ViewModels
{
    public class ProfessionalViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
        [Display(Name = "Apellido")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "La matricula es obligatoria.")]
        [StringLength(20, ErrorMessage = "La matricula no puede superar los 20 caracteres.")]
        [RegularExpression(@"^[A-Za-z0-9\-\/]+$", ErrorMessage = "La matricula solo puede contener letras, numeros, guion y barra.")]
        [Display(Name = "Matricula")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "La especialidad es obligatoria.")]
        [StringLength(100, ErrorMessage = "La especialidad no puede superar los 100 caracteres.")]
        [Display(Name = "Especialidad")]
        public string Especialidad { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActivo { get; set; } = true;

        public static ProfessionalViewModel FromEntity(Professional professional) => new()
        {
            Id = professional.Id,
            Nombre = professional.Nombre,
            Apellido = professional.Apellido,
            Matricula = professional.Matricula,
            Especialidad = professional.Especialidad,
            IsActivo = professional.IsActivo
        };

        public Professional ToEntity() => new()
        {
            Id = Id,
            Nombre = Nombre,
            Apellido = Apellido,
            Matricula = Matricula,
            Especialidad = Especialidad,
            IsActivo = IsActivo
        };
    }
}
