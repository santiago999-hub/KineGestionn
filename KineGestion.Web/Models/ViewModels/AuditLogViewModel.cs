using System;
using KineGestion.Core.Entities;

namespace KineGestion.Web.Models.ViewModels
{
    public class AuditLogViewModel
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }

        public static AuditLogViewModel FromEntity(AuditLog auditLog) => new()
        {
            Id = auditLog.Id,
            EntityName = auditLog.EntityName,
            EntityId = auditLog.EntityId,
            Action = auditLog.Action,
            ChangedBy = auditLog.ChangedBy,
            ChangedAt = auditLog.ChangedAt,
            OldValuesJson = auditLog.OldValuesJson,
            NewValuesJson = auditLog.NewValuesJson
        };
    }
}