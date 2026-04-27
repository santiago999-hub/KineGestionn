using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    public class SessionsController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IPatientService _patientService;
        private readonly IProfessionalService _professionalService;
        private readonly ITreatmentService _treatmentService;

        public SessionsController(
            ISessionService sessionService,
            IPatientService patientService,
            IProfessionalService professionalService,
            ITreatmentService treatmentService)
        {
            _sessionService = sessionService;
            _patientService = patientService;
            _professionalService = professionalService;
            _treatmentService = treatmentService;
        }

        public async Task<IActionResult> Index()
        {
            var sessions = await _sessionService.GetAllAsync();
            var viewModels = sessions.Select(SessionViewModel.FromEntity);
            return View(viewModels);
        }

        public async Task<IActionResult> Create()
        {
            var viewModel = new SessionViewModel
            {
                FechaHora = DateTime.Now
            };

            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SessionViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var session = viewModel.ToEntity();
                await _sessionService.CreateAsync(session);
                TempData["Success"] = "Sesion registrada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(viewModel.FechaHora), ex.Message);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        private async Task LoadSelectListsAsync(SessionViewModel viewModel)
        {
            var patients = await _patientService.GetAllAsync();
            var professionals = await _professionalService.GetActiveProfessionalsAsync();
            var treatments = await _treatmentService.GetAllAsync();

            viewModel.Pacientes = patients
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} - DNI {p.DNI}"
                })
                .OrderBy(p => p.Text)
                .ToList();

            viewModel.Profesionales = professionals
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} - Matricula {p.Matricula}"
                })
                .OrderBy(p => p.Text)
                .ToList();

            viewModel.Tratamientos = treatments
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Descripcion
                })
                .OrderBy(t => t.Text)
                .ToList();
        }
    }
}
