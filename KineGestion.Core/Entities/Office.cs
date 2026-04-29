using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class Office : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<Session> Sesiones { get; set; } = new List<Session>();
        public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
}
