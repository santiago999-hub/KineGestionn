using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RemindersController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IDataProtector _protector;
        private readonly IReminderDispatchQueue _reminderDispatchQueue;
        private readonly IReminderDeliveryService _reminderDeliveryService;
        private readonly IDispatchEventRepository _dispatchEventRepository;
        private readonly IBillingOperationalAlertService _billingOperationalAlertService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RemindersController> _logger;

        public RemindersController(
            ISessionService sessionService,
            IDataProtectionProvider dataProtectionProvider,
            IReminderDispatchQueue reminderDispatchQueue,
            IReminderDeliveryService reminderDeliveryService,
            IDispatchEventRepository dispatchEventRepository,
            IBillingOperationalAlertService billingOperationalAlertService,
            IConfiguration configuration,
            ILogger<RemindersController> logger)
        {
            _sessionService = sessionService;
            _protector = dataProtectionProvider.CreateProtector("KineGestion.ReminderLink.v1");
            _reminderDispatchQueue = reminderDispatchQueue;
            _reminderDeliveryService = reminderDeliveryService;
            _dispatchEventRepository = dispatchEventRepository;
            _billingOperationalAlertService = billingOperationalAlertService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int hoursAhead = 24)
        {
            if (hoursAhead < 1) hoursAhead = 1;
            if (hoursAhead > 168) hoursAhead = 168;

            var start = DateTime.UtcNow;
            var end = start.AddHours(hoursAhead);
            var operationalWindows = GetOperationalWindowsHours();

            var candidates = await _sessionService.GetReminderCandidatesAsync(start, end);
            var billingAlertSnapshot = await _billingOperationalAlertService.GetSnapshotAsync(start);

            var operationalCount = 0;
            if (operationalWindows.Count > 0)
            {
                var operationSet = new HashSet<int>();
                foreach (var windowHours in operationalWindows)
                {
                    if (windowHours <= hoursAhead)
                    {
                        // Ventana dentro del rango ya consultado: filtrar sin pegarle a la BD.
                        var windowEnd = start.AddHours(windowHours);
                        foreach (var c in candidates.Where(c => c.FechaHora < windowEnd))
                            operationSet.Add(c.SessionId);
                    }
                    else
                    {
                        var operationalCandidates = await _sessionService.GetReminderCandidatesAsync(start, start.AddHours(windowHours));
                        foreach (var c in operationalCandidates)
                            operationSet.Add(c.SessionId);
                    }
                }

                operationalCount = operationSet.Count;
            }

            var model = new ReminderCampaignViewModel
            {
                HoursAhead = hoursAhead,
                WindowStartUtc = start,
                WindowEndUtc = end,
                OperationalWindowsHours = operationalWindows,
                OperationalCandidatesCount = operationalCount,
                BillingBatchWarnThresholdPct = billingAlertSnapshot.ThresholdPct,
                HasBillingBatchConsecutiveLowWeeks = billingAlertSnapshot.HasConsecutiveLowWeeks,
                BillingBatchWeeklyTrendPoints = billingAlertSnapshot.TrendPoints,
                Items = candidates.Select(c => new ReminderItemViewModel
                {
                    SessionId = c.SessionId,
                    FechaHora = c.FechaHora,
                    PacienteNombre = c.PacienteNombre,
                    PacienteEmail = c.PacienteEmail,
                    PacienteTelefono = c.PacienteTelefono,
                    ProfesionalNombre = c.ProfesionalNombre,
                    TratamientoDescripcion = c.TratamientoDescripcion,
                    ConfirmUrl = BuildActionUrl(c.SessionId, "confirm", c.FechaHora),
                    CancelUrl = BuildActionUrl(c.SessionId, "cancel", c.FechaHora)
                }).ToList()
            };

            var history = await _dispatchEventRepository.GetByTypeAsync(DispatchTypes.PatientReminder, null, null, limit: 20);

            model.History = history.Select(MapHistoryItem).ToList();

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

            var queuedCount = 0;
            var errorMessages = new List<string>();

            foreach (var id in selectedIds)
            {
                if (!byId.TryGetValue(id, out var candidate))
                {
                    errorMessages.Add($"Sesión {id}: no está en la ventana de envío actual.");
                    continue;
                }

                var workItem = BuildWorkItem(candidate, User?.Identity?.Name);

                await _reminderDispatchQueue.QueueAsync(workItem);
                queuedCount++;
            }

            if (queuedCount > 0)
                TempData["Success"] = $"Recordatorios encolados: {queuedCount} de {selectedIds.Length}. Se procesarán en segundo plano.";

            if (errorMessages.Count > 0)
                TempData["Error"] = string.Join(" | ", errorMessages.Take(5));

            return RedirectToAction(nameof(Index), new { hoursAhead });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchOperational(int hoursAhead = 24)
        {
            var windows = GetOperationalWindowsHours();
            if (windows.Count == 0)
            {
                TempData["Error"] = "No hay ventanas operativas configuradas para envío.";
                return RedirectToAction(nameof(Index), new { hoursAhead });
            }

            var now = DateTime.UtcNow;
            var bySessionId = new Dictionary<int, SessionReminderCandidateDto>();

            foreach (var windowHours in windows)
            {
                var candidates = await _sessionService.GetReminderCandidatesAsync(now, now.AddHours(windowHours));
                foreach (var candidate in candidates)
                    bySessionId[candidate.SessionId] = candidate;
            }

            if (bySessionId.Count == 0)
            {
                TempData["Success"] = "No hay sesiones elegibles en ventanas operativas para enviar recordatorios.";
                return RedirectToAction(nameof(Index), new { hoursAhead });
            }

            foreach (var candidate in bySessionId.Values)
            {
                await _reminderDispatchQueue.QueueAsync(BuildWorkItem(candidate, User?.Identity?.Name));
            }

            var billingAlertDispatch = await _billingOperationalAlertService.QueueAlertIfNeededAsync(User?.Identity?.Name, now);

            TempData["Success"] = $"Recordatorios operativos encolados: {bySessionId.Count}. Ventanas: {string.Join(" + ", windows.Select(w => w + "h"))}.{(string.IsNullOrWhiteSpace(billingAlertDispatch.Message) ? string.Empty : " " + billingAlertDispatch.Message)}";
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
                ConfirmUrl = BuildActionUrl(candidate.SessionId, "confirm", candidate.FechaHora),
                CancelUrl = BuildActionUrl(candidate.SessionId, "cancel", candidate.FechaHora)
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

        private static ReminderDispatchHistoryItemViewModel MapHistoryItem(DispatchEvent dispatchEvent)
        {
            var item = new ReminderDispatchHistoryItemViewModel
            {
                ChangedAt = dispatchEvent.SentAtUtc,
                ChangedBy = dispatchEvent.ChangedBy,
                SessionId = dispatchEvent.SessionId ?? 0,
                ChannelSummary = BuildChannelSummary(dispatchEvent.EmailSent, dispatchEvent.WhatsAppSent),
                Status = (dispatchEvent.EmailSent || dispatchEvent.WhatsAppSent) ? "Enviado" : "Error",
                ErrorSummary = string.IsNullOrWhiteSpace(dispatchEvent.Errors) ? null : dispatchEvent.Errors
            };

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

        private ReminderDispatchWorkItem BuildWorkItem(SessionReminderCandidateDto candidate, string? changedBy)
        {
            return new ReminderDispatchWorkItem
            {
                SessionId = candidate.SessionId,
                FechaHora = candidate.FechaHora,
                PacienteNombre = candidate.PacienteNombre,
                PacienteEmail = candidate.PacienteEmail,
                PacienteTelefono = candidate.PacienteTelefono,
                ProfesionalNombre = candidate.ProfesionalNombre,
                TratamientoDescripcion = candidate.TratamientoDescripcion,
                ConfirmUrl = BuildActionUrl(candidate.SessionId, "confirm", candidate.FechaHora),
                CancelUrl = BuildActionUrl(candidate.SessionId, "cancel", candidate.FechaHora),
                ChangedBy = changedBy,
                EnqueuedAtUtc = DateTime.UtcNow
            };
        }

        private string BuildActionUrl(int sessionId, string action, DateTime sessionStartUtc)
        {
            // El enlace no puede actuar después de iniciado el turno: si la sesión ocurre
            // antes del límite genérico (2 días), la expiración se ata al horario del turno.
            // Así un link de confirmar/cancelar nunca queda válido para una sesión ya pasada.
            var cutoffUtc = DateTime.UtcNow.AddDays(2);
            var expiresUtc = sessionStartUtc < cutoffUtc ? sessionStartUtc : cutoffUtc;
            var payload = string.Join(
                "|",
                sessionId,
                action,
                expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                sessionStartUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            var token = _protector.Protect(payload);
            return Url.Action(nameof(Respond), "Reminders", new { sessionId, action, token }, Request.Scheme) ?? string.Empty;
        }

        private bool TryValidateToken(int sessionId, string action, string token)
        {
            try
            {
                var payload = _protector.Unprotect(token);

                // Formato legacy (3 partes): solo vencimiento. Formato actual (4 partes):
                // además el enlace queda invalidado a partir del inicio del turno.
                var parts = payload.Split('|');
                if (parts.Length != 3 && parts.Length != 4) return false;

                if (!int.TryParse(parts[0], out var tokenSessionId) || tokenSessionId != sessionId)
                    return false;

                if (!string.Equals(parts[1], action, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                    return false;

                var expiresUtc = new DateTime(ticks, DateTimeKind.Utc);
                if (DateTime.UtcNow > expiresUtc)
                    return false;

                if (parts.Length == 4)
                {
                    if (!long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionStartTicks))
                        return false;

                    var sessionStartUtc = new DateTime(sessionStartTicks, DateTimeKind.Utc);
                    if (DateTime.UtcNow >= sessionStartUtc)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<int> GetOperationalWindowsHours()
        {
            return OperationalConfig.ReadDistinctHourWindows(
                _configuration,
                _logger,
                "Reminders:OperationalWindowsHours",
                fallback: new[] { 24, 3 },
                min: 1,
                max: 168);
        }

    }
}
