using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using KineGestion.Core;
using KineGestion.Core.DTOs;
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
        private const int MaxExportRows = 5000;
        private const int MaxRangeDays = 366;

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

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            bool onlyCompletedPending = true,
            int page = 1,
            int pageSize = 10,
            SessionStatus? status = null,
            PaymentStatus? payment = null,
            bool filtersApplied = false)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var today = DateTime.UtcNow.Date;
            var (from, toExclusive) = ResolveDateRange(dateFrom, dateTo, today);

            var statusFilter = filtersApplied ? status : (onlyCompletedPending ? SessionStatus.Completed : null);
            var paymentFilter = filtersApplied ? payment : (onlyCompletedPending ? PaymentStatus.Pending : null);

            var pendingCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, from, toExclusive);
            var paidCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, from, toExclusive);
            var completedPaidCount = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(
                SessionStatus.Completed,
                PaymentStatus.Paid,
                from,
                toExclusive);
            var completedPendingCount = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(
                SessionStatus.Completed,
                PaymentStatus.Pending,
                from,
                toExclusive);

            var aging0To2Days = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, today.AddDays(-2), today.AddDays(1));
            var aging3To7Days = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, today.AddDays(-7), today.AddDays(-2));
            var aging8PlusDays = await _sessionService.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, DateTime.MinValue, today.AddDays(-7));

            var previousToExclusive = from;
            var previousFrom = from.AddDays(-(toExclusive - from).Days);
            var previousPaidCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, previousFrom, previousToExclusive);

            var (items, totalCount) = await _sessionService.GetPagedListForAdminAsync(
                page,
                pageSize,
                search,
                statusFilter,
                paymentFilter,
                from,
                toExclusive,
                "fecha",
                "desc");

            var defaultSessionAmount = Math.Max(0m, _configuration.GetValue<decimal?>("Billing:DefaultSessionAmount") ?? 0m);
            var dailyPoints = await BuildDailyPointsAsync(from, toExclusive, defaultSessionAmount);
            var professionalEffectiveness = await BuildProfessionalEffectivenessAsync(from, toExclusive, defaultSessionAmount);
            var topPendingPatients = await BuildTopPendingPatientsAsync(from, toExclusive, defaultSessionAmount, today);

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
                DateTo = toExclusive.AddDays(-1),
                OnlyCompletedPending = onlyCompletedPending,
                PendingCount = pendingCount,
                PaidCount = paidCount,
                CompletedPaidCount = completedPaidCount,
                CompletedPendingCount = completedPendingCount,
                Aging0To2DaysCount = aging0To2Days,
                Aging3To7DaysCount = aging3To7Days,
                Aging8PlusDaysCount = aging8PlusDays,
                DefaultSessionAmount = defaultSessionAmount,
                PreviousPaidAmount = previousPaidCount * defaultSessionAmount,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Search = search,
                StatusFilter = statusFilter,
                PaymentFilter = paymentFilter,
                FiltersApplied = filtersApplied,
                DailyPoints = dailyPoints,
                ProfessionalEffectiveness = professionalEffectiveness,
                TopPendingPatients = topPendingPatients,
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

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            bool onlyCompletedPending = true,
            SessionStatus? status = null,
            PaymentStatus? payment = null,
            bool filtersApplied = false)
        {
            var today = DateTime.UtcNow.Date;
            var (from, toExclusive) = ResolveDateRange(dateFrom, dateTo, today);
            var statusFilter = filtersApplied ? status : (onlyCompletedPending ? SessionStatus.Completed : null);
            var paymentFilter = filtersApplied ? payment : (onlyCompletedPending ? PaymentStatus.Pending : null);
            var defaultSessionAmount = Math.Max(0m, _configuration.GetValue<decimal?>("Billing:DefaultSessionAmount") ?? 0m);

            var (items, _) = await _sessionService.GetPagedListForAdminAsync(
                1,
                MaxExportRows,
                search,
                statusFilter,
                paymentFilter,
                from,
                toExclusive,
                "fecha",
                "desc");

            var builder = new StringBuilder();
            builder.AppendLine("Fecha;Paciente;Profesional;Tratamiento;Consultorio;Estado sesion;Estado pago;Importe estimado");
            foreach (var item in items)
            {
                builder.Append(CsvField(item.FechaHora.ToString("dd/MM/yyyy HH:mm"))).Append(';');
                builder.Append(CsvField(item.PacienteNombre)).Append(';');
                builder.Append(CsvField(item.ProfesionalNombre)).Append(';');
                builder.Append(CsvField(item.TratamientoDescripcion)).Append(';');
                builder.Append(CsvField(item.OfficeNombre)).Append(';');
                builder.Append(CsvField(item.Status.ToString())).Append(';');
                builder.Append(CsvField(item.PaymentStatus.ToString())).Append(';');
                builder.AppendLine((defaultSessionAmount * (item.PaymentStatus == PaymentStatus.Paid ? 1 : 0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }

            var fileName = $"cobranzas-{from:yyyyMMdd}-{toExclusive.AddDays(-1):yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(), "text/csv", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> MarkPaid(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10, SessionStatus? status = null, PaymentStatus? payment = null, bool filtersApplied = false)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Paid);
            TempData["Success"] = "Cobro marcado como pagado.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
        }

        [HttpPost]
        public async Task<IActionResult> MarkPending(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10, SessionStatus? status = null, PaymentStatus? payment = null, bool filtersApplied = false)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Pending);
            TempData["Success"] = "Cobro marcado como pendiente.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
        }

        [HttpPost]
        public async Task<IActionResult> MarkPaidBatch(List<int>? sessionIds, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10, SessionStatus? status = null, PaymentStatus? payment = null, bool filtersApplied = false)
        {
            var normalizedIds = (sessionIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión pendiente para marcar como pagada.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
            }

            var result = await _sessionService.MarkCompletedPendingAsPaidBatchAsync(normalizedIds);
            StoreBatchStats(normalizedIds.Count, result.UpdatedCount, result.SkippedCount);
            await LogBillingBatchAsync("MarkPaidBatch", normalizedIds.Count, result.UpdatedCount, result.SkippedCount, dateFrom, dateTo, search, onlyCompletedPending);

            if (result.UpdatedCount == 0)
            {
                TempData["Error"] = "No se encontraron sesiones completadas y pendientes para actualizar en la selección actual.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
            }

            TempData["Success"] = result.SkippedCount > 0
                ? $"{result.UpdatedCount} sesiones marcadas como pagadas y {result.SkippedCount} omitidas por no estar completadas/pedientes."
                : result.UpdatedCount == 1
                    ? "1 sesión marcada como pagada."
                    : $"{result.UpdatedCount} sesiones marcadas como pagadas.";

            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
        }

        [HttpPost]
        public async Task<IActionResult> MarkPendingBatch(List<int>? sessionIds, DateTime? dateFrom, DateTime? dateTo, string? search, bool onlyCompletedPending = true, int page = 1, int pageSize = 10, SessionStatus? status = null, PaymentStatus? payment = null, bool filtersApplied = false)
        {
            var normalizedIds = (sessionIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                TempData["Error"] = "Seleccioná al menos una sesión para reabrir en lote.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
            }

            var result = await _sessionService.MarkPaidAsPendingBatchAsync(normalizedIds);
            StoreBatchStats(normalizedIds.Count, result.UpdatedCount, result.SkippedCount);
            await LogBillingBatchAsync("MarkPendingBatch", normalizedIds.Count, result.UpdatedCount, result.SkippedCount, dateFrom, dateTo, search, onlyCompletedPending);

            if (result.UpdatedCount == 0)
            {
                TempData["Error"] = "No se encontraron sesiones pagadas para reabrir en la selección actual.";
                return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
            }

            TempData["Success"] = result.SkippedCount > 0
                ? $"{result.UpdatedCount} sesiones reabiertas y {result.SkippedCount} omitidas por no estar pagadas."
                : result.UpdatedCount == 1
                    ? "1 sesión reabierta como pendiente."
                    : $"{result.UpdatedCount} sesiones reabiertas como pendientes.";

            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize, status, payment, filtersApplied });
        }

        private static (DateTime From, DateTime ToExclusive) ResolveDateRange(DateTime? dateFrom, DateTime? dateTo, DateTime today)
        {
            var from = (dateFrom ?? today.AddDays(-29)).Date;
            var toExclusive = (dateTo ?? today).Date.AddDays(1);

            if (toExclusive <= from)
                toExclusive = from.AddDays(1);

            if ((toExclusive - from).TotalDays > MaxRangeDays)
                from = toExclusive.AddDays(-MaxRangeDays);

            return (from, toExclusive);
        }

        private async Task<List<BillingDailyPointViewModel>> BuildDailyPointsAsync(DateTime from, DateTime toExclusive, decimal amount)
        {
            var series = await _sessionService.GetDailySessionSeriesAsync(from, toExclusive) ?? Array.Empty<DailySessionCountDto>();
            var byDay = series.GroupBy(p => p.Day.Date).ToDictionary(g => g.Key, g => g.First());

            var points = new List<BillingDailyPointViewModel>();
            for (var day = from; day < toExclusive; day = day.AddDays(1))
            {
                byDay.TryGetValue(day, out var point);
                var paid = point?.Paid ?? 0;
                points.Add(new BillingDailyPointViewModel
                {
                    Day = day,
                    Label = day.ToString("dd/MM"),
                    PaidCount = paid,
                    CompletedCount = point?.Completed ?? 0,
                    CollectedAmount = paid * amount
                });
            }

            return points;
        }

        private async Task<List<BillingProfessionalViewModel>> BuildProfessionalEffectivenessAsync(DateTime from, DateTime toExclusive, decimal amount)
        {
            var segments = await _sessionService.GetKpiSegmentsByProfessionalAsync(from, toExclusive) ?? Array.Empty<KpiSegmentDto>();

            return segments
                .Where(s => s.Completed > 0 || s.CompletedPending > 0)
                .OrderByDescending(s => s.CompletedPending)
                .ThenByDescending(s => s.Completed)
                .Take(6)
                .Select(s => new BillingProfessionalViewModel
                {
                    ProfessionalName = s.SegmentLabel,
                    Completed = s.Completed,
                    CompletedPending = s.CompletedPending,
                    CompletedPaid = s.CompletedPaid,
                    PendingAmount = s.CompletedPending * amount
                })
                .ToList();
        }

        private async Task<List<BillingTopDebtViewModel>> BuildTopPendingPatientsAsync(DateTime from, DateTime toExclusive, decimal amount, DateTime today)
        {
            var debts = await _sessionService.GetTopPendingPatientsAsync(from, toExclusive, 5) ?? Array.Empty<PendingPatientDebtDto>();

            return debts
                .Select(d => new BillingTopDebtViewModel
                {
                    PatientLabel = d.PatientLabel,
                    Telefono = d.Telefono,
                    PendingCount = d.PendingCount,
                    PendingAmount = d.PendingCount * amount,
                    AgeDays = Math.Max(0, (today - d.OldestPendingUtc.Date).Days)
                })
                .ToList();
        }

        private static string CsvField(string? value)
        {
            var text = value ?? string.Empty;
            return text.Contains(';') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
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
