using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DispatchQueueController : Controller
    {
        private readonly IDispatchJobRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DispatchQueueController> _logger;

        public DispatchQueueController(
            IDispatchJobRepository repository,
            IConfiguration configuration,
            ILogger<DispatchQueueController> logger)
        {
            _repository = repository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            DispatchJobStatus? status = null,
            string? dispatchType = null,
            string? search = null,
            int page = 1)
        {
            var pageSize = _configuration.GetValue<int?>("DispatchQueue:AdminPageSize") ?? 20;
            if (pageSize is < 5 or > 200)
                pageSize = 20;

            if (page < 1)
                page = 1;

            var stuckThreshold = DateTime.UtcNow.AddMinutes(-StuckAlertThresholdMinutes());
            var stats = await _repository.GetStatsAsync(stuckThreshold, HttpContext.RequestAborted);
            var (items, totalCount) = await _repository.GetJobsAsync(
                status,
                dispatchType,
                search,
                page,
                pageSize,
                HttpContext.RequestAborted);
            var dispatchTypes = await _repository.GetDistinctDispatchTypesAsync(HttpContext.RequestAborted);

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            var model = new DispatchQueueIndexViewModel
            {
                Stats = stats,
                Jobs = items,
                TotalCount = totalCount,
                Page = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                FilterStatus = status,
                FilterDispatchType = dispatchType,
                Search = search,
                DispatchTypes = dispatchTypes
            };

            if (stats.StuckCount > 0)
                TempData["Error"] = $"{stats.StuckCount} jobs pendientes llevan más de {StuckAlertThresholdMinutes()} minutos sin ser procesados.";

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetrySelected(int[] ids)
        {
            var count = await _repository.ResetForRetryAsync(ids ?? Array.Empty<int>(), DateTime.UtcNow, HttpContext.RequestAborted);

            TempData[count > 0 ? "Success" : "Error"] = count > 0
                ? $"Reintentos programados: {count}."
                : "No había jobs fallidos para reintentar.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryAllFailed()
        {
            var failedIds = (await _repository.GetJobsAsync(
                DispatchJobStatus.Failed,
                null,
                null,
                1,
                1000,
                HttpContext.RequestAborted)).Items.Select(j => j.Id).ToArray();

            var count = await _repository.ResetForRetryAsync(failedIds, DateTime.UtcNow, HttpContext.RequestAborted);

            TempData[count > 0 ? "Success" : "Error"] = count > 0
                ? $"Reintentos programados: {count}."
                : "No había jobs fallidos para reintentar.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSelected(int[] ids)
        {
            var count = await _repository.CancelAsync(ids ?? Array.Empty<int>(), DateTime.UtcNow, HttpContext.RequestAborted);

            TempData[count > 0 ? "Success" : "Error"] = count > 0
                ? $"Envíos cancelados: {count}."
                : "No había envíos pendientes o en proceso para cancelar.";

            return RedirectToAction(nameof(Index));
        }

        private int StuckAlertThresholdMinutes()
        {
            var value = _configuration.GetValue<int?>("DispatchQueue:StuckAlertThresholdMinutes") ?? 30;
            return value is < 5 or > 1440 ? 30 : value;
        }
    }
}