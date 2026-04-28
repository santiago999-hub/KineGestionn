using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Office
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del consultorio es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
        public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
}
