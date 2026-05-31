using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using KineGestion.Core;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IConfiguration _configuration;

        public BillingController(ISessionService sessionService, IConfiguration configuration)
        {
            _sessionService = sessionService;
            _configuration = configuration;
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

            foreach (var sessionId in normalizedIds)
            {
                await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Paid);
            }

            TempData["Success"] = normalizedIds.Count == 1
                ? "1 sesión marcada como pagada."
                : $"{normalizedIds.Count} sesiones marcadas como pagadas.";

            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, onlyCompletedPending, page, pageSize });
        }
    }
}
