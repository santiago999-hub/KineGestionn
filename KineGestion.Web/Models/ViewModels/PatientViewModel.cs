using System;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core.Entities;

namespace KineGestion.Web.Models.ViewModels
{
    /// <summary>
    /// ViewModel de Patient: contiene solo los datos que la Vista necesita mostrar o recibir.
    /// Actúa como "filtro de seguridad" entre la entidad de dominio y la interfaz de usuario.
    /// Las Navigation Properties (Sesiones) nunca se exponen aquí — la Vista no las necesita.
    /// </summary>
    public class PatientViewModel
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

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [StringLength(8, MinimumLength = 7, ErrorMessage = "El DNI debe tener entre 7 y 8 dígitos.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "El DNI solo debe contener dígitos.")]
        [Display(Name = "DNI")]
        public string DNI { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [StringLength(150)]
        [Display(Name = "Obra Social")]
        public string? ObraSocial { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono no es válido.")]
        [StringLength(20)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El formato del email no es válido.")]
        [StringLength(150)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Activo")]
        public bool IsActivo { get; set; } = true;

        public string CreatedBy { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // ── Métodos de Mapping (Manual, sin AutoMapper) ────────────────────────
        // El Controller llama a estos métodos para convertir entre capas.
        // La entidad nunca llega a la Vista; el ViewModel nunca llega a la BD.

        /// <summary>Convierte una entidad Patient en un PatientViewModel para la Vista.</summary>
        public static PatientViewModel FromEntity(Patient patient) => new()
        {
            Id               = patient.Id,
            Nombre           = patient.Nombre,
            Apellido         = patient.Apellido,
            DNI              = patient.DNI,
            FechaNacimiento  = patient.FechaNacimiento,
            ObraSocial       = patient.ObraSocial,
            Telefono         = patient.Telefono,
            Email            = patient.Email,
            IsActivo         = patient.IsActivo,
            CreatedBy        = patient.CreatedBy,
            UpdatedBy        = patient.UpdatedBy,
            CreatedAt        = patient.CreatedAt,
            UpdatedAt        = patient.UpdatedAt
        };

        /// <summary>Convierte este ViewModel en una entidad Patient para la BD.</summary>
        public Patient ToEntity() => new()
        {
            Id               = Id,
            Nombre           = Nombre,
            Apellido         = Apellido,
            DNI              = DNI,
            FechaNacimiento  = FechaNacimiento ?? DateTime.Today,
            ObraSocial       = ObraSocial,
            Telefono         = Telefono,
            Email            = Email,
            IsActivo         = IsActivo
        };
    }
}
