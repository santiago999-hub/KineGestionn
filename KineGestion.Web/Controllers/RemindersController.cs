using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RemindersController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IDataProtector _protector;
        private readonly IReminderDeliveryService _reminderDeliveryService;
        private readonly IAuditLogService _auditLogService;

        public RemindersController(
            ISessionService sessionService,
            IDataProtectionProvider dataProtectionProvider,
            IReminderDeliveryService reminderDeliveryService,
            IAuditLogService auditLogService)
        {
            _sessionService = sessionService;
            _protector = dataProtectionProvider.CreateProtector("KineGestion.ReminderLink.v1");
            _reminderDeliveryService = reminderDeliveryService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index(int hoursAhead = 24)
        {
            if (hoursAhead < 1) hoursAhead = 1;
            if (hoursAhead > 168) hoursAhead = 168;

            var start = DateTime.UtcNow;
            var end = start.AddHours(hoursAhead);

            var candidates = await _sessionService.GetReminderCandidatesAsync(start, end);

            var model = new ReminderCampaignViewModel
            {
                HoursAhead = hoursAhead,
                WindowStartUtc = start,
                WindowEndUtc = end,
                Items = candidates.Select(c => new ReminderItemViewModel
                {
                    SessionId = c.SessionId,
                    FechaHora = c.FechaHora,
                    PacienteNombre = c.PacienteNombre,
                    PacienteEmail = c.PacienteEmail,
                    PacienteTelefono = c.PacienteTelefono,
                    ProfesionalNombre = c.ProfesionalNombre,
                    TratamientoDescripcion = c.TratamientoDescripcion,
                    ConfirmUrl = BuildActionUrl(c.SessionId, "confirm"),
                    CancelUrl = BuildActionUrl(c.SessionId, "cancel")
                }).ToList()
            };

            var history = await _auditLogService.GetPagedAsync(
                entityName: "ReminderDispatch",
                entityId: null,
                changedBy: null,
                action: "Create",
                dateFrom: null,
                dateTo: null,
                page: 1,
                pageSize: 20);

            model.History = history.Items.Select(MapHistoryItem).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchSelected(int hoursAhead, int[] selectedSessionIds)
        {
            if (hoursAhead < 1) hoursAhead = 1;
            if (hoursAhead > 168) hoursAhead = 168;

            var selectedIds = (selectedSessionIds ?? Array.Empty<int>()).Distinct().ToArray();
            if (selectedIds.Length == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión para enviar recordatorios.";
                return RedirectToAction(nameof(Index), new { hoursAhead });
            }

            var start = DateTime.UtcNow;
            var end = start.AddHours(hoursAhead);
            var candidates = await _sessionService.GetReminderCandidatesAsync(start, end);
            var byId = candidates.ToDictionary(c => c.SessionId);

            var successCount = 0;
            var errorMessages = new List<string>();

            foreach (var id in selectedIds)
            {
                if (!byId.TryGetValue(id, out var candidate))
                {
                    errorMessages.Add($"Sesión {id}: no está en la ventana de envío actual.");
                    continue;
                }

                var request = new ReminderDeliveryRequest
                {
                    SessionId = candidate.SessionId,
                    FechaHora = candidate.FechaHora,
                    PacienteNombre = candidate.PacienteNombre,
                    PacienteEmail = candidate.PacienteEmail,
                    PacienteTelefono = candidate.PacienteTelefono,
                    ProfesionalNombre = candidate.ProfesionalNombre,
                    TratamientoDescripcion = candidate.TratamientoDescripcion,
                    ConfirmUrl = BuildActionUrl(candidate.SessionId, "confirm"),
                    CancelUrl = BuildActionUrl(candidate.SessionId, "cancel")
                };

                var dispatchResult = await _reminderDeliveryService.SendAsync(request);

                var actor = User?.Identity?.Name;
                await _auditLogService.AddAsync(new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = candidate.SessionId.ToString(CultureInfo.InvariantCulture),
                    Action = "Create",
                    ChangedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
                    ChangedAt = DateTime.UtcNow,
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        candidate.SessionId,
                        candidate.FechaHora,
                        candidate.PacienteNombre,
                        candidate.PacienteEmail,
                        candidate.PacienteTelefono,
                        EmailSent = dispatchResult.EmailSent,
                        WhatsAppSent = dispatchResult.WhatsAppSent,
                        Errors = dispatchResult.Errors
                    })
                });

                if (dispatchResult.AnyChannelSent)
                {
                    successCount++;
                    continue;
                }

                if (dispatchResult.Errors.Count > 0)
                    errorMessages.AddRange(dispatchResult.Errors.Take(2));
                else
                    errorMessages.Add($"Sesión {id}: no se pudo enviar por ningún canal.");
            }

            if (successCount > 0)
                TempData["Success"] = $"Recordatorios enviados: {successCount} de {selectedIds.Length}.";

            if (errorMessages.Count > 0)
                TempData["Error"] = string.Join(" | ", errorMessages.Take(5));

            return RedirectToAction(nameof(Index), new { hoursAhead });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTest(int hoursAhead, int? sessionId, string? testEmail, string? testPhone, bool dryRun = true)
        {
            if (hoursAhead < 1) hoursAhead = 1;
            if (hoursAhead > 168) hoursAhead = 168;

            var start = DateTime.UtcNow;
            var end = start.AddHours(hoursAhead);
            var candidates = await _sessionService.GetReminderCandidatesAsync(start, end);

            var candidate = sessionId.HasValue
                ? candidates.FirstOrDefault(c => c.SessionId == sessionId.Value)
                : candidates.FirstOrDefault();

            if (candidate is null)
            {
                TempData["Error"] = "No hay sesiones disponibles en la ventana para ejecutar una prueba.";
                return RedirectToAction(nameof(Index), new { hoursAhead });
            }

            var request = new ReminderDeliveryRequest
            {
                SessionId = candidate.SessionId,
                FechaHora = candidate.FechaHora,
                PacienteNombre = candidate.PacienteNombre,
                PacienteEmail = string.IsNullOrWhiteSpace(testEmail) ? candidate.PacienteEmail : testEmail.Trim(),
                PacienteTelefono = string.IsNullOrWhiteSpace(testPhone) ? candidate.PacienteTelefono : testPhone.Trim(),
                ProfesionalNombre = candidate.ProfesionalNombre,
                TratamientoDescripcion = candidate.TratamientoDescripcion,
                ConfirmUrl = BuildActionUrl(candidate.SessionId, "confirm"),
                CancelUrl = BuildActionUrl(candidate.SessionId, "cancel")
            };

            var preview = _reminderDeliveryService.BuildPreview(request);

            var model = new ReminderTestResultViewModel
            {
                DryRun = dryRun,
                SessionId = candidate.SessionId,
                PacienteNombre = candidate.PacienteNombre,
                DestinoEmail = request.PacienteEmail,
                DestinoWhatsApp = request.PacienteTelefono,
                EmailSubject = preview.EmailSubject,
                EmailBody = preview.EmailBody,
                WhatsAppBody = preview.WhatsAppBody,
                CanEmail = preview.CanEmail,
                CanWhatsApp = preview.CanWhatsApp,
                Warnings = preview.Warnings.ToList()
            };

            if (!dryRun)
            {
                var sendResult = await _reminderDeliveryService.SendAsync(request);
                model.EmailSent = sendResult.EmailSent;
                model.WhatsAppSent = sendResult.WhatsAppSent;
                model.Errors = sendResult.Errors.ToList();
            }

            return View("TestResult", model);
        }

        private static ReminderDispatchHistoryItemViewModel MapHistoryItem(AuditLog log)
        {
            var item = new ReminderDispatchHistoryItemViewModel
            {
                ChangedAt = log.ChangedAt,
                ChangedBy = log.ChangedBy,
                SessionId = int.TryParse(log.EntityId, out var sessionId) ? sessionId : 0,
                ChannelSummary = "-",
                Status = "Error",
                ErrorSummary = null
            };

            try
            {
                if (!string.IsNullOrWhiteSpace(log.NewValuesJson))
                {
                    using var doc = JsonDocument.Parse(log.NewValuesJson);
                    var root = doc.RootElement;
                    var emailSent = root.TryGetProperty("EmailSent", out var emailProp) && emailProp.GetBoolean();
                    var whatsappSent = root.TryGetProperty("WhatsAppSent", out var waProp) && waProp.GetBoolean();

                    item.ChannelSummary = BuildChannelSummary(emailSent, whatsappSent);
                    item.Status = (emailSent || whatsappSent) ? "Enviado" : "Error";

                    if (root.TryGetProperty("Errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Array)
                    {
                        var errors = errorsProp
                            .EnumerateArray()
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Take(2)
                            .ToList();
                        item.ErrorSummary = errors.Count == 0 ? null : string.Join(" | ", errors);
                    }
                }
            }
            catch
            {
                item.ErrorSummary = "No se pudo interpretar el detalle del evento.";
            }

            return item;
        }

        private static string BuildChannelSummary(bool emailSent, bool whatsappSent)
        {
            if (emailSent && whatsappSent) return "Email + WhatsApp";
            if (emailSent) return "Email";
            if (whatsappSent) return "WhatsApp";
            return "Sin envío";
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Respond(int sessionId, string action, string token)
        {
            if (!TryValidateToken(sessionId, action, token))
            {
                return View("Result", new ReminderResponseViewModel
                {
                    Success = false,
                    Title = "Enlace inválido",
                    Message = "El enlace no es válido o expiró. Solicitá un nuevo recordatorio."
                });
            }

            try
            {
                if (string.Equals(action, "confirm", StringComparison.OrdinalIgnoreCase))
                {
                    await _sessionService.ConfirmByReminderAsync(sessionId);
                    return View("Result", new ReminderResponseViewModel
                    {
                        Success = true,
                        Title = "Asistencia confirmada",
                        Message = "Tu sesión quedó confirmada. Te esperamos."
                    });
                }

                if (string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase))
                {
                    await _sessionService.CancelByReminderAsync(sessionId);
                    return View("Result", new ReminderResponseViewModel
                    {
                        Success = true,
                        Title = "Sesión cancelada",
                        Message = "La sesión fue cancelada correctamente."
                    });
                }
            }
            catch
            {
                return View("Result", new ReminderResponseViewModel
                {
                    Success = false,
                    Title = "No fue posible procesar",
                    Message = "No se pudo procesar el recordatorio. Intentá nuevamente."
                });
            }

            return View("Result", new ReminderResponseViewModel
            {
                Success = false,
                Title = "Acción no reconocida",
                Message = "La acción solicitada no es válida."
            });
        }

        private string BuildActionUrl(int sessionId, string action)
        {
            var expiresUtc = DateTime.UtcNow.AddDays(2);
            var payload = string.Join("|", sessionId, action, expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            var token = _protector.Protect(payload);
            return Url.Action(nameof(Respond), "Reminders", new { sessionId, action, token }, Request.Scheme) ?? string.Empty;
        }

        private bool TryValidateToken(int sessionId, string action, string token)
        {
            try
            {
                var payload = _protector.Unprotect(token);
                var parts = payload.Split('|');
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out var tokenSessionId) || tokenSessionId != sessionId)
                    return false;

                if (!string.Equals(parts[1], action, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                    return false;

                var expiresUtc = new DateTime(ticks, DateTimeKind.Utc);
                return DateTime.UtcNow <= expiresUtc;
            }
            catch
            {
                return false;
            }
        }
    }
}
