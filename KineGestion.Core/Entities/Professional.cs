using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Professional
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del profesional es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La matrícula es obligatoria.")]
        [StringLength(20, ErrorMessage = "La matrícula no puede superar los 20 caracteres.")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "La especialidad es obligatoria.")]
        [StringLength(100, ErrorMessage = "La especialidad no puede superar los 100 caracteres.")]
        public string Especialidad { get; set; } = string.Empty;

        // Navigation Property
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
    }
}
