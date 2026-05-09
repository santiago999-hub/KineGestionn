using System;

namespace KineGestion.Core.Entities
{
    public abstract class BaseEntity
    {
        public string CreatedBy { get; set; } = "system";
        public string UpdatedBy { get; set; } = "system";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}