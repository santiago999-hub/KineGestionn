using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Patient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [StringLength(8, MinimumLength = 7, ErrorMessage = "El DNI debe tener entre 7 y 8 dígitos.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "El DNI solo debe contener dígitos.")]
        public string DNI { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime FechaNacimiento { get; set; }

        [StringLength(150, ErrorMessage = "La obra social no puede superar los 150 caracteres.")]
        public string? ObraSocial { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono no es válido.")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El formato del email no es válido.")]
        [StringLength(150)]
        public string? Email { get; set; }

        /// <summary>
        /// Indica si el paciente está activo en el sistema.
        /// Un paciente inactivo fue dado de alta o archivado pero sus datos se conservan.
        /// </summary>
        public bool IsActivo { get; set; } = true;

        // Navigation Property
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
    }
}
