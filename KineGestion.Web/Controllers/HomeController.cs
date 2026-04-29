using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models;
using KineGestion.Web.Models.ViewModels;
using System.Linq;

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
        var countPatients = _patientService.CountActiveAsync();
        var countProfessionals = _professionalService.CountActiveAsync();
        var countTreatments = _treatmentService.CountAsync();
        var countSessions = _sessionService.CountAsync();

        await System.Threading.Tasks.Task.WhenAll(countPatients, countProfessionals, countTreatments, countSessions);

        var model = new HomeDashboardViewModel
        {
            PacientesActivosCount = countPatients.Result,
            ProfesionalesActivosCount = countProfessionals.Result,
            TratamientosCount = countTreatments.Result,
            SesionesCount = countSessions.Result
        };

        return View(model);
    }

    public IActionResult Privacy()
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
