using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IConfiguration _configuration;
        private readonly IBillingBatchEventRepository _billingBatchEventRepository;
        private readonly ILogger<BillingController> _logger;

        public BillingController(ISessionService sessionService, IConfiguration configuration, IBillingBatchEventRepository billingBatchEventRepository, ILogger<BillingController> logger)
        {
            _sessionService = sessionService;
            _configuration = configuration;
            _billingBatchEventRepository = billingBatchEventRepository;
            _logger = logger;
        }

        public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var from = (dateFrom ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var to = (dateTo ?? DateTime.UtcNow.Date).Date.AddDays(1);

            var pendingCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, from, to);
            var paidCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, from, to);
            var completedPendingCount = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(
                SessionStatus.Completed,
                PaymentStatus.Pending,
                from,
                to);

            var today = DateTime.UtcNow.Date;
            var aging0To2Days = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, today.AddDays(-2), today.AddDays(1));
            var aging3To7Days = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, today.AddDays(-7), today.AddDays(-2));
            var aging8PlusDays = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, DateTime.MinValue, today.AddDays(-7));

            SessionStatus? statusFilter = onlyCompletedPending ? SessionStatus.Completed : null;
            PaymentStatus? paymentFilter = onlyCompletedPending ? PaymentStatus.Pending : null;

            var (items, totalCount) = await _sessionService.GetPagedListForAdminAsync(
                page,
                pageSize,
                search,
                statusFilter,
                paymentFilter,
                from,
                to,
                "fecha",
                "desc");

            var defaultSessionAmount = Math.Max(0m, _configuration.GetValue<decimal?>("Billing:DefaultSessionAmount") ?? 0m);
            var batchWarnThresholdPct = Math.Clamp(_configuration.GetValue<decimal?>("Billing:BatchEffectivenessWarnThresholdPct") ?? 70m, 0m, 100m);

            var weeklyBatchTo = DateTime.UtcNow.Date;
            var weeklyBatchFrom = weeklyBatchTo.AddDays(-6);
            var weeklyBatchEvents = await _billingBatchEventRepository.GetByDateRangeAsync(weeklyBatchFrom, weeklyBatchTo);

            var weeklyRuns = 0;
            var weeklyRequested = 0;
            var weeklyUpdated = 0;
            var weeklySkipped = 0;

            foreach (var batchEvent in weeklyBatchEvents)
            {
                weeklyRuns++;
                weeklyRequested += batchEvent.RequestedCount;
                weeklyUpdated += batchEvent.UpdatedCount;
                weeklySkipped += batchEvent.SkippedCount;
            }

            var weeklyTrendPoints = BuildWeeklyTrend(weeklyBatchEvents, weeklyBatchTo, weeks: 4);
            var hasTwoConsecutiveLowWeeks = HasConsecutiveLowWeeks(weeklyTrendPoints, batchWarnThresholdPct, requiredConsecutiveWeeks: 2);

            if (hasTwoConsecutiveLowWeeks)
            {
                _logger.LogWarning(
                    "Billing batch effectiveness below threshold for two consecutive weeks. Threshold={ThresholdPct}, LatestWeekEffectiveness={LatestWeekEffectivenessPct}",
                    batchWarnThresholdPct,
                    weeklyTrendPoints.LastOrDefault()?.EffectivenessPct ?? 0m);
            }

            var model = new BillingDashboardViewModel
            {
                DateFrom = from,
                DateTo = to.AddDays(-1),
                OnlyCompletedPending = onlyCompletedPending,
                PendingCount = pendingCount,
                PaidCount = paidCount,
                CompletedPendingCount = completedPendingCount,
                Aging0To2DaysCount = aging0To2Days,
                Aging3To7DaysCount = aging3To7Days,
                Aging8PlusDaysCount = aging8PlusDays,
                DefaultSessionAmount = defaultSessionAmount,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Search = search,
                LastBatchRequestedCount = ReadTempDataInt("BillingBatchRequestedCount"),
                LastBatchUpdatedCount = ReadTempDataInt("BillingBatchUpdatedCount"),
                LastBatchSkippedCount = ReadTempDataInt("BillingBatchSkippedCount"),
                WeeklyBatchFrom = weeklyBatchFrom,
                WeeklyBatchTo = weeklyBatchTo,
                WeeklyBatchRuns = weeklyRuns,
                WeeklyBatchRequestedCount = weeklyRequested,
                WeeklyBatchUpdatedCount = weeklyUpdated,
                WeeklyBatchSkippedCount = weeklySkipped,
                WeeklyBatchWarnThresholdPct = batchWarnThresholdPct,
                HasTwoConsecutiveLowWeeks = hasTwoConsecutiveLowWeeks,
                WeeklyTrendPoints = weeklyTrendPoints,
                Items = items.Select(SessionViewModel.FromDto).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Paid);
            TempData["Success"] = "Cobro marcado como pagado.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPending(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Pending);
            TempData["Success"] = "Cobro marcado como pendiente.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaidBatch(List<int>? sessionIds, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10)
        {
            var normalizedIds = (sessionIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión pendiente para marcar como pagada.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
            }

            var result = await _sessionService.MarkCompletedPendingAsPaidBatchAsync(normalizedIds);
            StoreBatchStats(normalizedIds.Count, result.UpdatedCount, result.SkippedCount);
            await LogBillingBatchAsync("MarkPaidBatch", normalizedIds.Count, result.UpdatedCount, result.SkippedCount, dateFrom, dateTo, search, onlyCompletedPending);

            if (result.UpdatedCount == 0)
            {
                TempData["Error"] = "No se encontraron sesiones completadas y pendientes para actualizar en la selección actual.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
            }

            TempData["Success"] = result.SkippedCount > 0
                ? $"{result.UpdatedCount} sesiones marcadas como pagadas y {result.SkippedCount} omitidas por no estar completadas/pedientes."
                : result.UpdatedCount == 1
                    ? "1 sesión marcada como pagada."
                    : $"{result.UpdatedCount} sesiones marcadas como pagadas.";

            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPendingBatch(List<int>? sessionIds, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10)
        {
            var normalizedIds = (sessionIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión para reabrir en lote.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
            }

            var result = await _sessionService.MarkPaidAsPendingBatchAsync(normalizedIds);
            StoreBatchStats(normalizedIds.Count, result.UpdatedCount, result.SkippedCount);
            await LogBillingBatchAsync("MarkPendingBatch", normalizedIds.Count, result.UpdatedCount, result.SkippedCount, dateFrom, dateTo, search, onlyCompletedPending);

            if (result.UpdatedCount == 0)
            {
                TempData["Error"] = "No se encontraron sesiones pagadas para reabrir en la selección actual.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
            }

            TempData["Success"] = result.SkippedCount > 0
                ? $"{result.UpdatedCount} sesiones reabiertas y {result.SkippedCount} omitidas por no estar pagadas."
                : result.UpdatedCount == 1
                    ? "1 sesión reabierta como pendiente."
                    : $"{result.UpdatedCount} sesiones reabiertas como pendientes.";

            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
        }

        private int? ReadTempDataInt(string key)
        {
            if (!TempData.TryGetValue(key, out var value) || value is null)
                return null;

            return value switch
            {
                int intValue => intValue,
                string text when int.TryParse(text, out var parsed) => parsed,
                _ => null
            };
        }

        private void StoreBatchStats(int requestedCount, int updatedCount, int skippedCount)
        {
            TempData["BillingBatchRequestedCount"] = requestedCount;
            TempData["BillingBatchUpdatedCount"] = updatedCount;
            TempData["BillingBatchSkippedCount"] = skippedCount;
        }

        private async Task LogBillingBatchAsync(
            string operation,
            int requestedCount,
            int updatedCount,
            int skippedCount,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            bool onlyCompletedPending)
        {
            try
            {
                await _billingBatchEventRepository.AddAsync(new BillingBatchEvent
                {
                    Operation = operation,
                    RequestedCount = requestedCount,
                    UpdatedCount = updatedCount,
                    SkippedCount = skippedCount,
                    FilterDateFrom = dateFrom?.Date,
                    FilterDateTo = dateTo?.Date,
                    FilterSearch = TruncateSearch(search),
                    OnlyCompletedPending = onlyCompletedPending,
                    ChangedBy = AuditActor.Truncate(User?.Identity?.Name) ?? "system",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            catch
            {
                // El registro del evento operativo no debe interrumpir la operación de cobranza.
            }
        }

        private static List<BillingBatchTrendPointViewModel> BuildWeeklyTrend(IEnumerable<BillingBatchEvent> events, DateTime endDateInclusiveUtc, int weeks)
        {
            var points = new List<BillingBatchTrendPointViewModel>();

            for (var i = weeks - 1; i >= 0; i--)
            {
                var weekStart = endDateInclusiveUtc.Date.AddDays(-(i * 7) - 6);
                var weekEnd = weekStart.AddDays(6);

                var point = new BillingBatchTrendPointViewModel
                {
                    Label = $"{weekStart:dd/MM}-{weekEnd:dd/MM}"
                };

                foreach (var batchEvent in events.Where(e => e.CreatedAtUtc.Date >= weekStart && e.CreatedAtUtc.Date <= weekEnd))
                {
                    point.RequestedCount += batchEvent.RequestedCount;
                    point.UpdatedCount += batchEvent.UpdatedCount;
                    point.SkippedCount += batchEvent.SkippedCount;
                }

                points.Add(point);
            }

            return points;
        }

        private static bool HasConsecutiveLowWeeks(
            IReadOnlyCollection<BillingBatchTrendPointViewModel> points,
            decimal thresholdPct,
            int requiredConsecutiveWeeks)
        {
            if (points.Count == 0 || requiredConsecutiveWeeks <= 1)
                return false;

            var consecutive = 0;
            foreach (var point in points)
            {
                var isLow = point.RequestedCount > 0 && point.EffectivenessPct < thresholdPct;
                consecutive = isLow ? consecutive + 1 : 0;

                if (consecutive >= requiredConsecutiveWeeks)
                    return true;
            }

            return false;
        }

        private static string? TruncateSearch(string? search)
            => search is null || search.Length <= 200 ? search : search[..200];
    }
}
