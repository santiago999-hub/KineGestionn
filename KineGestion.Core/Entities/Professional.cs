using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Professional : BaseEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del profesional es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido del profesional es obligatorio.")]
        [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "La matrícula es obligatoria.")]
        [StringLength(20, ErrorMessage = "La matrícula no puede superar los 20 caracteres.")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "La especialidad es obligatoria.")]
        [StringLength(100, ErrorMessage = "La especialidad no puede superar los 100 caracteres.")]
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
