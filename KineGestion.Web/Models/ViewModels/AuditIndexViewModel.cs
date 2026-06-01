using System.Collections.Generic;
using System;
using System.Globalization;
using System.Resources;
using KineGestion.Core;

namespace KineGestion.Web.Models.ViewModels
{
    public class AuditIndexViewModel
    {
        private static readonly ResourceManager AuditLabels = new(
            "KineGestion.Web.Resources.AuditLabels",
            typeof(AuditIndexViewModel).Assembly);

        public static readonly IReadOnlyList<AuditEntityType> DefaultEntityOptions = new[]
        {
            AuditEntityType.Patient,
            AuditEntityType.Professional,
            AuditEntityType.Treatment,
            AuditEntityType.Session,
            AuditEntityType.Office,
            AuditEntityType.Equipment,
            AuditEntityType.BillingBatch
        };

        public static readonly IReadOnlyList<AuditActionType> DefaultActionOptions = new[]
        {
            AuditActionType.Create,
            AuditActionType.Update,
            AuditActionType.Delete
        };

        public IEnumerable<AuditLogViewModel> Items { get; set; } = new List<AuditLogViewModel>();
        public IReadOnlyList<AuditEntityType> EntityOptions { get; set; } = DefaultEntityOptions;
        public IReadOnlyList<AuditActionType> ActionOptions { get; set; } = DefaultActionOptions;
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public string? ChangedBy { get; set; }
        public string? Action { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount <= 0 || PageSize <= 0
            ? 1
            : (int)System.Math.Ceiling((double)TotalCount / PageSize);

        public static string GetActionLabel(AuditActionType action) => action switch
        {
            AuditActionType.Create => Translate("Audit.Action.Create", "Alta"),
            AuditActionType.Update => Translate("Audit.Action.Update", "Edición"),
            AuditActionType.Delete => Translate("Audit.Action.Delete", "Baja"),
            _ => action.ToString()
        };

        public static string GetActionLabel(string? actionValue)
        {
            if (string.IsNullOrWhiteSpace(actionValue))
                return string.Empty;

            return Enum.TryParse<AuditActionType>(actionValue, ignoreCase: true, out var action)
                ? GetActionLabel(action)
                : actionValue;
        }

        public static string GetEntityLabel(AuditEntityType entity) => entity switch
        {
            AuditEntityType.Patient => Translate("Audit.Entity.Patient", "Patient"),
            AuditEntityType.Professional => Translate("Audit.Entity.Professional", "Professional"),
            AuditEntityType.Treatment => Translate("Audit.Entity.Treatment", "Treatment"),
            AuditEntityType.Session => Translate("Audit.Entity.Session", "Session"),
            AuditEntityType.Office => Translate("Audit.Entity.Office", "Office"),
            AuditEntityType.Equipment => Translate("Audit.Entity.Equipment", "Equipment"),
            AuditEntityType.BillingBatch => Translate("Audit.Entity.BillingBatch", "Billing batch"),
            _ => entity.ToString()
        };

        public static string GetEntityLabel(string? entityValue)
        {
            if (string.IsNullOrWhiteSpace(entityValue))
                return string.Empty;

            return Enum.TryParse<AuditEntityType>(entityValue, ignoreCase: true, out var entity)
                ? GetEntityLabel(entity)
                : entityValue;
        }

        private static string Translate(string key, string fallback)
        {
            return AuditLabels.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }
    }
}