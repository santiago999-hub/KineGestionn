using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReminderFunnelController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IDispatchEventRepository _dispatchEventRepository;

        public ReminderFunnelController(ISessionService sessionService, IDispatchEventRepository dispatchEventRepository)
        {
            _sessionService = sessionService;
            _dispatchEventRepository = dispatchEventRepository;
        }

        public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = (dateFrom ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var to = (dateTo ?? DateTime.UtcNow.Date).Date.AddDays(1);
            var toExclusive = to;

            var dispatches = await _dispatchEventRepository.GetByTypeAsync(DispatchTypes.PatientReminder, from, to.AddDays(-1));

            var sentSessionIds = new HashSet<int>();
            foreach (var dispatchEvent in dispatches)
            {
                if (!dispatchEvent.SessionId.HasValue)
                    continue;

                if (dispatchEvent.EmailSent || dispatchEvent.WhatsAppSent)
                    sentSessionIds.Add(dispatchEvent.SessionId.Value);
            }

            var funnel = await _sessionService.BuildReminderFunnelAsync(
                sentSessionIds,
                from,
                toExclusive);

            var model = new ReminderFunnelDashboardViewModel
            {
                DateFrom = from,
                DateToExclusive = toExclusive,
                Funnel = funnel
            };

            return View(model);
        }
    }
}