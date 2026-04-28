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
        private readonly IOfficeService _officeService;

        public SessionsController(
            ISessionService sessionService,
            IPatientService patientService,
            IProfessionalService professionalService,
            ITreatmentService treatmentService,
            IOfficeService officeService)
        {
            _sessionService = sessionService;
            _patientService = patientService;
            _professionalService = professionalService;
            _treatmentService = treatmentService;
            _officeService = officeService;
        }

        // Listado administrativo: no incluye Evolution
        public async Task<IActionResult> Index()
        {
            var sessions = await _sessionService.GetAllForAdminAsync();
            var viewModels = sessions.Select(SessionViewModel.FromEntityForAdmin);
            return View(viewModels);
        }

        public async Task<IActionResult> Create(int? patientId = null)
        {
            var viewModel = new SessionViewModel
            {
                FechaHora = DateTime.Now,
                PacienteId = patientId ?? 0
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

        // GET: /Sessions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            var viewModel = SessionViewModel.FromEntity(session);
            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Sessions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SessionViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var session = viewModel.ToEntity();
                await _sessionService.UpdateAsync(session);
                TempData["Success"] = "Sesion actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(viewModel.FechaHora), ex.Message);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /Sessions/Details/5
        // Detalle para profesionales: incluye Evolution.
        public async Task<IActionResult> Details(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            return View(SessionViewModel.FromEntity(session));
        }

        // GET: /Sessions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            return View(SessionViewModel.FromEntityForAdmin(session));
        }

        // POST: /Sessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _sessionService.DeleteAsync(id);
            TempData["Success"] = "Sesion eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSelectListsAsync(SessionViewModel viewModel)
        {
            var patients = await _patientService.GetAllAsync();
            var professionals = await _professionalService.GetActiveProfessionalsAsync();
            var treatments = await _treatmentService.GetAllAsync();
            var offices = await _officeService.GetActiveAsync();

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

            viewModel.Consultorios = offices
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name
                })
                .ToList();
        }
    }
}
