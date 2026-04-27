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
