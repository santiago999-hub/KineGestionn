using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingFollowUpController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IBillingFollowUpService _billingFollowUpService;
        private readonly IReminderDispatchQueue _reminderDispatchQueue;
        private readonly IReminderDeliveryService _reminderDeliveryService;
        private readonly IAuditLogService _auditLogService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BillingFollowUpController> _logger;

        public BillingFollowUpController(
            ISessionService sessionService,
            IBillingFollowUpService billingFollowUpService,
            IReminderDispatchQueue reminderDispatchQueue,
            IReminderDeliveryService reminderDeliveryService,
            IAuditLogService auditLogService,
            IConfiguration configuration,
            ILogger<BillingFollowUpController> logger)
        {
            _sessionService = sessionService;
            _billingFollowUpService = billingFollowUpService;
            _reminderDispatchQueue = reminderDispatchQueue;
            _reminderDeliveryService = reminderDeliveryService;
            _auditLogService = auditLogService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var asOfUtc = DateTime.UtcNow;
            var candidates = (await _sessionService.GetBillingFollowUpCandidatesAsync(asOfUtc, MinAgeDays(), MaxAgeDays())).ToList();
            var campaign = _billingFollowUpService.BuildCampaign(candidates);

            var model = new BillingFollowUpCampaignViewModel
            {
                AsOfUtc = asOfUtc,
                MinAgeDays = MinAgeDays(),
                MaxAgeDays = MaxAgeDays(),
                TotalCandidates = campaign.Candidates.Count,
                TierGroups = campaign.Tiers.Select(tier =>
                {
                    var tierCandidates = campaign.Candidates.Values
                        .Where(c => tier.Matches(c.PaymentAgeDays))
                        .OrderByDescending(c => c.PaymentAgeDays)
                        .ThenBy(c => c.FechaHora)
                        .ToList();

                    return new BillingFollowUpTierGroupViewModel
                    {
                        Label = tier.Label,
                        BadgeClass = tier.BadgeClass,
                        MinAgeDays = tier.MinAgeDays,
                        MaxAgeDays = tier.MaxAgeDays,
                        Items = tierCandidates.Select(c => MapItem(c, tier)).ToList()
                    };
                }).ToList()
            };

            var history = await _auditLogService.GetPagedAsync(
                entityName: "BillingFollowUp",
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
        public async Task<IActionResult> DispatchSelected(int[] selectedSessionIds)
        {
            var selectedIds = (selectedSessionIds ?? Array.Empty<int>()).Distinct().ToArray();
            if (selectedIds.Length == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión para enviar la campaña de cobro.";
                return RedirectToAction(nameof(Index));
            }

            var asOfUtc = DateTime.UtcNow;
            var candidates = (await _sessionService.GetBillingFollowUpCandidatesAsync(asOfUtc, MinAgeDays(), MaxAgeDays())).ToList();
            var campaign = _billingFollowUpService.BuildCampaign(candidates);

            var queuedCount = 0;
            var errorMessages = new List<string>();

            foreach (var id in selectedIds)
            {
                if (!campaign.Candidates.TryGetValue(id, out var candidate))
                {
                    errorMessages.Add($"Sesión {id}: ya no es elegible para la campaña.");
                    continue;
                }

                var tier = campaign.GetTierForCandidate(candidate);
                if (tier is null)
                {
                    errorMessages.Add($"Sesión {id}: no tiene un nivel de cobro asignado (antigüedad {candidate.PaymentAgeDays}d).");
                    continue;
                }

                await _reminderDispatchQueue.QueueAsync(BuildWorkItem(candidate, tier));
                queuedCount++;
            }

            if (queuedCount > 0)
                TempData["Success"] = $"Envíos encolados: {queuedCount} de {selectedIds.Length}. Se procesarán en segundo plano.";

            if (errorMessages.Count > 0)
                TempData["Error"] = string.Join(" | ", errorMessages.Take(5));

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchTier(int minAgeDays, int maxAgeDays)
        {
            var asOfUtc = DateTime.UtcNow;
            var candidates = (await _sessionService.GetBillingFollowUpCandidatesAsync(asOfUtc, MinAgeDays(), MaxAgeDays())).ToList();
            var campaign = _billingFollowUpService.BuildCampaign(candidates);

            var tier = campaign.Tiers.FirstOrDefault(t => t.MinAgeDays == minAgeDays && t.MaxAgeDays == maxAgeDays);
            if (tier is null)
            {
                TempData["Error"] = "Nivel de cobro no reconocido.";
                return RedirectToAction(nameof(Index));
            }

            var eligible = campaign.Candidates.Values.Where(c => tier.Matches(c.PaymentAgeDays)).ToList();
            if (eligible.Count == 0)
            {
                TempData["Success"] = $"No hay sesiones elegibles en el nivel {tier.Label}.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var candidate in eligible)
            {
                await _reminderDispatchQueue.QueueAsync(BuildWorkItem(candidate, tier));
            }

            TempData["Success"] = $"Enviados {eligible.Count} en nivel {tier.Label}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTest(int? sessionId, string? testEmail, string? testPhone, bool dryRun = true)
        {
            var asOfUtc = DateTime.UtcNow;
            var candidates = (await _sessionService.GetBillingFollowUpCandidatesAsync(asOfUtc, MinAgeDays(), MaxAgeDays())).ToList();
            var campaign = _billingFollowUpService.BuildCampaign(candidates);

            var candidate = sessionId.HasValue && campaign.Candidates.TryGetValue(sessionId.Value, out var picked)
                ? picked
                : campaign.Candidates.Values.OrderBy(c => c.PaymentAgeDays).FirstOrDefault();

            if (candidate is null)
            {
                TempData["Error"] = "No hay sesiones disponibles para ejecutar una prueba de campaña.";
                return RedirectToAction(nameof(Index));
            }

            var tier = campaign.GetTierForCandidate(candidate);
            if (tier is null)
            {
                TempData["Error"] = "La sesión de muestra no tiene nivel de cobro asignado.";
                return RedirectToAction(nameof(Index));
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
                EmailSubjectOverride = tier.EmailSubject,
                EmailBodyOverride = tier.EmailBody,
                WhatsAppBodyOverride = tier.WhatsAppBody
            };

            var preview = _reminderDeliveryService.BuildPreview(request);

            var model = new BillingFollowUpTestResultViewModel
            {
                DryRun = dryRun,
                SessionId = candidate.SessionId,
                PacienteNombre = candidate.PacienteNombre,
                TierLabel = tier.Label,
                PaymentAgeDays = candidate.PaymentAgeDays,
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

        private ReminderDispatchWorkItem BuildWorkItem(BillingFollowUpCandidateDto candidate, BillingFollowUpTierDefinition tier)
            => new ReminderDispatchWorkItem
            {
                SessionId = candidate.SessionId,
                FechaHora = candidate.FechaHora,
                PacienteNombre = candidate.PacienteNombre,
                PacienteEmail = candidate.PacienteEmail,
                PacienteTelefono = candidate.PacienteTelefono,
                ProfesionalNombre = candidate.ProfesionalNombre,
                TratamientoDescripcion = candidate.TratamientoDescripcion,
                ChangedBy = User?.Identity?.Name,
                EnqueuedAtUtc = DateTime.UtcNow,
                DispatchType = $"BillingFollowUp:{tier.Tier}",
                EmailSubjectOverride = tier.EmailSubject,
                EmailBodyOverride = tier.EmailBody,
                WhatsAppBodyOverride = tier.WhatsAppBody,
                AuditEntityName = "BillingFollowUp"
            };

        private static BillingFollowUpItemViewModel MapItem(BillingFollowUpCandidateDto c, BillingFollowUpTierDefinition tier)
            => new BillingFollowUpItemViewModel
            {
                SessionId = c.SessionId,
                FechaHora = c.FechaHora,
                PaymentAgeDays = c.PaymentAgeDays,
                PacienteNombre = c.PacienteNombre,
                PacienteEmail = c.PacienteEmail,
                PacienteTelefono = c.PacienteTelefono,
                ProfesionalNombre = c.ProfesionalNombre,
                TratamientoDescripcion = c.TratamientoDescripcion,
                EmailSubject = tier.EmailSubject,
                EmailBody = tier.EmailBody,
                WhatsAppBody = tier.WhatsAppBody
            };

        private static BillingFollowUpDispatchHistoryItemViewModel MapHistoryItem(AuditLog log)
        {
            var item = new BillingFollowUpDispatchHistoryItemViewModel
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

                    item.ChannelSummary = (emailSent && whatsappSent)
                        ? "Email + WhatsApp"
                        : emailSent ? "Email" : whatsappSent ? "WhatsApp" : "Sin envío";
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

        private int MinAgeDays()
            => Math.Max(0, _configuration.GetValue<int?>("BillingFollowUp:MinAgeDays") ?? 1);

        private int MaxAgeDays()
            => Math.Max(MinAgeDays(), _configuration.GetValue<int?>("BillingFollowUp:MaxAgeDays") ?? 9999);
    }
}
