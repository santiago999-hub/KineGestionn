using System;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var from = (dateFrom ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var to = (dateTo ?? DateTime.UtcNow.Date).Date.AddDays(1);

            var pendingCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, from, to);
            var paidCount = await _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, from, to);

            var (items, totalCount) = await _sessionService.GetPagedListForAdminAsync(
                page,
                pageSize,
                search,
                null,
                null,
                from,
                to,
                "fecha",
                "desc");

            var defaultSessionAmount = Math.Max(0m, _configuration.GetValue<decimal?>("Billing:DefaultSessionAmount") ?? 0m);

            var model = new BillingDashboardViewModel
            {
                DateFrom = from,
                DateTo = to.AddDays(-1),
                PendingCount = pendingCount,
                PaidCount = paidCount,
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
        public async Task<IActionResult> MarkPaid(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1, int pageSize = 10)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Paid);
            TempData["Success"] = "Cobro marcado como pagado.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPending(int sessionId, DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1, int pageSize = 10)
        {
            await _sessionService.SetPaymentStatusAsync(sessionId, PaymentStatus.Pending);
            TempData["Success"] = "Cobro marcado como pendiente.";
            return RedirectToAction(nameof(Index), new { dateFrom, dateTo, search, page, pageSize });
        }
    }
}
