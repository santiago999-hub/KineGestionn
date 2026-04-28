using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Treatment : BaseEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La descripción del tratamiento es obligatoria.")]
        [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(1, 365, ErrorMessage = "La cantidad de sesiones debe estar entre 1 y 365.")]
        public int CantidadSesionesTotales { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        // Foreign Key — un tratamiento pertenece a un paciente específico
        [Required]
        public int PatientId { get; set; }

        // Navigation Properties
        public virtual Patient? Patient { get; set; }
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
    }
}
