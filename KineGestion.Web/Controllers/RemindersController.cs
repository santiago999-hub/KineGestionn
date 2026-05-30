using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

        public RemindersController(
            ISessionService sessionService,
            IDataProtectionProvider dataProtectionProvider,
            IReminderDeliveryService reminderDeliveryService)
        {
            _sessionService = sessionService;
            _protector = dataProtectionProvider.CreateProtector("KineGestion.ReminderLink.v1");
            _reminderDeliveryService = reminderDeliveryService;
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
