using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Treatment : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Descripcion { get; set; } = string.Empty;

        public int CantidadSesionesTotales { get; set; }

        public DateTime FechaInicio { get; set; }

        // Foreign Key — un tratamiento pertenece a un paciente específico
        [Required]
        public int PatientId { get; set; }

        // Navigation Properties
        public virtual Patient? Patient { get; set; }
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
    }
}
