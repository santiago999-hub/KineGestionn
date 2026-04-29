using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Professional : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Apellido { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Matricula { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Especialidad { get; set; } = string.Empty;

        /// <summary>
        /// Indica si el profesional se encuentra activo en el sistema.
        /// Se usa para borrado lógico y conservación de historial clínico.
        /// </summary>
        public bool IsActivo { get; set; } = true;

        // Navigation Property
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
    }
}
