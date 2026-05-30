using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
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

        public RemindersController(ISessionService sessionService, IDataProtectionProvider dataProtectionProvider)
        {
            _sessionService = sessionService;
            _protector = dataProtectionProvider.CreateProtector("KineGestion.ReminderLink.v1");
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
