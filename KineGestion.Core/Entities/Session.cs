using System;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Session
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La fecha y hora de la sesión es obligatoria.")]
        public DateTime FechaHora { get; set; }

        [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar los 1000 caracteres.")]
        public string? Observaciones { get; set; }

        public bool EstadoPago { get; set; } = false;

        /// <summary>
        /// Número de esta sesión dentro del tratamiento (ej: 3 de 10).
        /// Permite mostrar el progreso del paciente.
        /// </summary>
        [Range(1, 365, ErrorMessage = "El número de sesión debe ser mayor a 0.")]
        public int NroSesionEnTratamiento { get; set; }

        // Foreign Keys
        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ProfessionalId { get; set; }

        [Required]
        public int TreatmentId { get; set; }

        // Navigation Properties
        public virtual Patient? Patient { get; set; }
        public virtual Professional? Professional { get; set; }
        public virtual Treatment? Treatment { get; set; }
    }
}
