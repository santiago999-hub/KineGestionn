using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Patient : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Apellido { get; set; } = string.Empty;

        [Required]
        [StringLength(8, MinimumLength = 7)]
        public string DNI { get; set; } = string.Empty;

        [Required]
        public DateTime FechaNacimiento { get; set; }

        [Required]
        [StringLength(150)]
        public string ObraSocial { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Telefono { get; set; }

        [StringLength(150)]
        public string? Email { get; set; }

        /// <summary>
        /// Indica si el paciente está activo en el sistema.
        /// Un paciente inactivo fue dado de alta o archivado pero sus datos se conservan.
        /// </summary>
        public bool IsActivo { get; set; } = true;

        // Navigation Properties
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
        public virtual ICollection<Treatment> Tratamientos { get; set; } = new List<Treatment>();
    }
}
