using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReminderFunnelController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IAuditLogService _auditLogService;

        public ReminderFunnelController(ISessionService sessionService, IAuditLogService auditLogService)
        {
            _sessionService = sessionService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = (dateFrom ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var to = (dateTo ?? DateTime.UtcNow.Date).Date.AddDays(1);
            var toExclusive = to;

            var dispatches = (await _auditLogService.GetAllAsync(
                entityName: "ReminderDispatch",
                entityId: null,
                changedBy: null,
                action: "Create",
                dateFrom: from,
                dateTo: to.AddDays(-1))).ToList();

            var sentSessionIds = new HashSet<int>();
            foreach (var log in dispatches)
            {
                if (!int.TryParse(log.EntityId, out var sessionId))
                    continue;

                if (!string.IsNullOrWhiteSpace(log.NewValuesJson)
                    && IsPatientReminder(log.NewValuesJson)
                    && WasActuallySent(log.NewValuesJson))
                {
                    sentSessionIds.Add(sessionId);
                }
            }

            var funnel = await _sessionService.BuildReminderFunnelAsync(
                sentSessionIds,
                from,
                toExclusive);

            var model = new ReminderFunnelDashboardViewModel
            {
                DateFrom = from,
                DateToExclusive = toExclusive,
                Funnel = funnel
            };

            return View(model);
        }

        private static bool IsPatientReminder(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("DispatchType", out var dispatchType))
                {
                    var value = dispatchType.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                        return true;
                    return value.Equals("PatientReminder", StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static bool WasActuallySent(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var emailSent = root.TryGetProperty("EmailSent", out var emailProp) && emailProp.ValueKind == JsonValueKind.True;
                var whatsappSent = root.TryGetProperty("WhatsAppSent", out var waProp) && waProp.ValueKind == JsonValueKind.True;

                return emailSent || whatsappSent;
            }
            catch
            {
                return false;
            }
        }
    }
}