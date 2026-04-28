using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models;
using KineGestion.Web.Models.ViewModels;
using System.Linq;

namespace KineGestion.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IPatientService _patientService;
    private readonly IProfessionalService _professionalService;

    public HomeController(
        ILogger<HomeController> logger,
        IPatientService patientService,
        IProfessionalService professionalService)
    {
        _logger = logger;
        _patientService = patientService;
        _professionalService = professionalService;
    }

    public async Task<IActionResult> Index()
    {
        var activePatients = await _patientService.GetAllAsync();
        var activeProfessionals = await _professionalService.GetActiveProfessionalsAsync();

        var model = new HomeDashboardViewModel
        {
            PacientesActivosCount = activePatients.Count(),
            ProfesionalesActivosCount = activeProfessionals.Count()
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
