using System;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Core.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string EntityId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string ChangedBy { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; }

        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
    }
}