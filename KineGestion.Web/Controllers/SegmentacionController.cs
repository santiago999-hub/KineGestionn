using System;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SegmentacionController : Controller
    {
        private readonly ISessionService _sessionService;

        public SegmentacionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = (dateFrom ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var to = (dateTo ?? DateTime.UtcNow.Date).Date.AddDays(1);

            var byProfessional = await _sessionService.GetKpiSegmentsByProfessionalAsync(from, to);
            var byTimeSlot = await _sessionService.GetKpiSegmentsByTimeSlotAsync(from, to);

            ViewBag.DateFrom = from;
            ViewBag.DateTo = to.AddDays(-1);
            ViewBag.ByProfessional = byProfessional;
            ViewBag.ByTimeSlot = byTimeSlot;

            return View();
        }
    }
}
