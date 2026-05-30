using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models;
using KineGestion.Web.Models.ViewModels;
using System.Linq;
using KineGestion.Core;
using System;

namespace KineGestion.Web.Controllers;

[Authorize(Roles = "Admin,Kinesiologo")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IPatientService _patientService;
    private readonly IProfessionalService _professionalService;
    private readonly ITreatmentService _treatmentService;
    private readonly ISessionService _sessionService;

    public HomeController(
        ILogger<HomeController> logger,
        IPatientService patientService,
        IProfessionalService professionalService,
        ITreatmentService treatmentService,
        ISessionService sessionService)
    {
        _logger = logger;
        _patientService = patientService;
        _professionalService = professionalService;
        _treatmentService = treatmentService;
        _sessionService = sessionService;
    }

    public async Task<IActionResult> Index()
    {
        var countPatients = await SafeCountAsync(() => _patientService.CountActiveAsync(), nameof(_patientService.CountActiveAsync));
        var countProfessionals = await SafeCountAsync(() => _professionalService.CountActiveAsync(), nameof(_professionalService.CountActiveAsync));
        var countTreatments = await SafeCountAsync(() => _treatmentService.CountAsync(), nameof(_treatmentService.CountAsync));
        var countSessions = await SafeCountAsync(() => _sessionService.CountAsync(), nameof(_sessionService.CountAsync));

        var today = DateTime.UtcNow;
        var countToday = await SafeCountAsync(() => _sessionService.CountTodayAsync(today), nameof(_sessionService.CountTodayAsync));
        var countCompletedToday = await SafeCountAsync(() => _sessionService.CountByStatusOnDateAsync(SessionStatus.Completed, today), nameof(_sessionService.CountByStatusOnDateAsync));
        var countPendingPago = await SafeCountAsync(() => _sessionService.CountByPaymentStatusAsync(PaymentStatus.Pending), nameof(_sessionService.CountByPaymentStatusAsync));
        var countPendingStatus = await SafeCountAsync(() => _sessionService.CountByStatusAsync(SessionStatus.Pending), nameof(_sessionService.CountByStatusAsync));

        var rangeFrom = DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = DateTime.UtcNow.Date.AddDays(1);
        var totalLast30 = await SafeCountAsync(() => _sessionService.CountInRangeAsync(rangeFrom, rangeTo), nameof(_sessionService.CountInRangeAsync));
        var paidLast30 = await SafeCountAsync(() => _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, rangeFrom, rangeTo), nameof(_sessionService.CountByPaymentStatusInRangeAsync));
        var canceledLast30 = await SafeCountAsync(() => _sessionService.CountByStatusInRangeAsync(SessionStatus.Canceled, rangeFrom, rangeTo), nameof(_sessionService.CountByStatusInRangeAsync));

        var completionRateToday = countToday == 0 ? 0m : Math.Round((decimal)countCompletedToday * 100m / countToday, 2);
        var collectionRateLast30 = totalLast30 == 0 ? 0m : Math.Round((decimal)paidLast30 * 100m / totalLast30, 2);
        var cancellationRateLast30 = totalLast30 == 0 ? 0m : Math.Round((decimal)canceledLast30 * 100m / totalLast30, 2);

        var model = new HomeDashboardViewModel
        {
            PacientesActivosCount = countPatients,
            ProfesionalesActivosCount = countProfessionals,
            TratamientosCount = countTreatments,
            SesionesCount = countSessions,
            SesionesHoyCount = countToday,
            SesionesCompletadasHoyCount = countCompletedToday,
            SesionesPendientesPagoCount = countPendingPago,
            SesionesPendientesConfirmacionCount = countPendingStatus,
            CompletionRateToday = completionRateToday,
            CollectionRateLast30Days = collectionRateLast30,
            CancellationRateLast30Days = cancellationRateLast30
        };

        return View(model);

        async Task<int> SafeCountAsync(Func<Task<int>> action, string source)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando métrica del dashboard desde {Source}", source);
                return 0;
            }
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult QuienesSomos()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string? friendlyMessage = null)
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            FriendlyMessage = string.IsNullOrWhiteSpace(friendlyMessage)
                ? "Ocurrió un error inesperado al procesar la solicitud."
                : friendlyMessage
        });
    }
}
