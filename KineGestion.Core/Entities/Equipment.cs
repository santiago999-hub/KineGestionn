using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Equipment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del equipamiento es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Consultorio al que pertenece el equipo (opcional — puede ser portátil o sin asignación fija).
        /// </summary>
        public int? OfficeId { get; set; }

        // Navigation
        public virtual Office? Office { get; set; }
    }
}
