using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models;
using KineGestion.Web.Models.ViewModels;
using System.Linq;
using KineGestion.Core;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace KineGestion.Web.Controllers;

[Authorize(Roles = "Admin,Kinesiologo")]
public class HomeController : Controller
{
    private const string DashboardCacheKey = "HomeController.Index.Dashboard";

    private readonly ILogger<HomeController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IPatientService _patientService;
    private readonly IProfessionalService _professionalService;
    private readonly ITreatmentService _treatmentService;
    private readonly ISessionService _sessionService;

    public HomeController(
        ILogger<HomeController> logger,
        IMemoryCache memoryCache,
        IPatientService patientService,
        IProfessionalService professionalService,
        ITreatmentService treatmentService,
        ISessionService sessionService)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _patientService = patientService;
        _professionalService = professionalService;
        _treatmentService = treatmentService;
        _sessionService = sessionService;
    }

    public async Task<IActionResult> Index()
    {
        if (_memoryCache.TryGetValue(DashboardCacheKey, out HomeDashboardViewModel? cachedModel) && cachedModel is not null)
            return View(cachedModel);

        var hasErrors = 0;

        var today = DateTime.UtcNow;
        var rangeFrom = DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = DateTime.UtcNow.Date.AddDays(1);

        var countPatientsTask = SafeCountAsync(() => _patientService.CountActiveAsync(), nameof(_patientService.CountActiveAsync));
        var countProfessionalsTask = SafeCountAsync(() => _professionalService.CountActiveAsync(), nameof(_professionalService.CountActiveAsync));
        var countTreatmentsTask = SafeCountAsync(() => _treatmentService.CountAsync(), nameof(_treatmentService.CountAsync));
        var countSessionsTask = SafeCountAsync(() => _sessionService.CountAsync(), nameof(_sessionService.CountAsync));
        var countTodayTask = SafeCountAsync(() => _sessionService.CountTodayAsync(today), nameof(_sessionService.CountTodayAsync));
        var countCompletedTodayTask = SafeCountAsync(() => _sessionService.CountByStatusOnDateAsync(SessionStatus.Completed, today), nameof(_sessionService.CountByStatusOnDateAsync));
        var countPendingPagoTask = SafeCountAsync(() => _sessionService.CountByPaymentStatusAsync(PaymentStatus.Pending), nameof(_sessionService.CountByPaymentStatusAsync));
        var countPendingStatusTask = SafeCountAsync(() => _sessionService.CountByStatusAsync(SessionStatus.Pending), nameof(_sessionService.CountByStatusAsync));
        var totalLast30Task = SafeCountAsync(() => _sessionService.CountInRangeAsync(rangeFrom, rangeTo), nameof(_sessionService.CountInRangeAsync));
        var paidLast30Task = SafeCountAsync(() => _sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, rangeFrom, rangeTo), nameof(_sessionService.CountByPaymentStatusInRangeAsync));
        var canceledLast30Task = SafeCountAsync(() => _sessionService.CountByStatusInRangeAsync(SessionStatus.Canceled, rangeFrom, rangeTo), nameof(_sessionService.CountByStatusInRangeAsync));

        await Task.WhenAll(
            countPatientsTask,
            countProfessionalsTask,
            countTreatmentsTask,
            countSessionsTask,
            countTodayTask,
            countCompletedTodayTask,
            countPendingPagoTask,
            countPendingStatusTask,
            totalLast30Task,
            paidLast30Task,
            canceledLast30Task);

        var countPatients = countPatientsTask.Result;
        var countProfessionals = countProfessionalsTask.Result;
        var countTreatments = countTreatmentsTask.Result;
        var countSessions = countSessionsTask.Result;
        var countToday = countTodayTask.Result;
        var countCompletedToday = countCompletedTodayTask.Result;
        var countPendingPago = countPendingPagoTask.Result;
        var countPendingStatus = countPendingStatusTask.Result;
        var totalLast30 = totalLast30Task.Result;
        var paidLast30 = paidLast30Task.Result;
        var canceledLast30 = canceledLast30Task.Result;

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

        if (hasErrors == 0)
        {
            _memoryCache.Set(DashboardCacheKey, model, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(15)
            });
        }

        return View(model);

        async Task<int> SafeCountAsync(Func<Task<int>> action, string source)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref hasErrors, 1);
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
