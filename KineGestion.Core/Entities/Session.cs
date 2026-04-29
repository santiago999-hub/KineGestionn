using System;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core;

namespace KineGestion.Core.Entities
{
    public class Session : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        public DateTime FechaHora { get; set; }

        [StringLength(1000)]
        public string? Observaciones { get; set; }

        [StringLength(4000)]
        public string? Evolution { get; set; }

        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        public SessionStatus Status { get; set; } = SessionStatus.Pending;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public int NroSesionEnTratamiento { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ProfessionalId { get; set; }

        [Required]
        public int TreatmentId { get; set; }

        public int? OfficeId { get; set; }

        /// <summary>
        /// Fecha en que se firmó/bloqueó la evolución clínica.
        /// Una vez establecida, el campo Evolution no puede modificarse.
        /// </summary>
        public DateTime? EvolutionLockedAt { get; set; }

        public virtual Patient? Patient { get; set; }
        public virtual Professional? Professional { get; set; }
        public virtual Treatment? Treatment { get; set; }
        public virtual Office? Office { get; set; }
    }
}
