using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PatientsController : Controller
    {
        private readonly IPatientService _patientService;
        private readonly ITreatmentService _treatmentService;
        private readonly ISessionService _sessionService;

        // ASP.NET Core inyecta automáticamente IPatientService
        // gracias al registro AddScoped en Program.cs.
        public PatientsController(IPatientService patientService, ITreatmentService treatmentService, ISessionService sessionService)
        {
            _patientService = patientService;
            _treatmentService = treatmentService;
            _sessionService = sessionService;
        }

        // GET: /Patients
        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (patients, totalCount) = await _patientService.GetPagedAsync(page, pageSize, search);
            var viewModels = patients.Select(PatientViewModel.FromEntity).ToList();

            var model = new PatientIndexViewModel
            {
                Items = viewModels,
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        // GET: /Patients/Create
        public IActionResult Create()
        {
            return View(new PatientViewModel());
        }

        // POST: /Patients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PatientViewModel viewModel)
        {
            // Valida las Data Annotations del ViewModel antes de continuar
            if (!ModelState.IsValid)
                return View(viewModel);

            try
            {
                var patient = viewModel.ToEntity();
                await _patientService.CreateAsync(patient);
                TempData["Success"] = $"Paciente {viewModel.Nombre} {viewModel.Apellido} registrado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? nameof(viewModel.DNI) : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
        }

        // GET: /Patients/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient is null)
                return NotFound();

            return View(PatientViewModel.FromEntity(patient));
        }

        // POST: /Patients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PatientViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(viewModel);

            try
            {
                var patient = viewModel.ToEntity();
                await _patientService.UpdateAsync(patient);
                TempData["Success"] = "Paciente actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? nameof(viewModel.DNI) : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
        }

        // GET: /Patients/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient is null)
                return NotFound();

            var treatmentsTask = _treatmentService.GetByPatientIdAsync(id);
            var sessionsTask = _sessionService.GetByPatientIdAsync(id);
            await System.Threading.Tasks.Task.WhenAll(treatmentsTask, sessionsTask);

            var model = new PatientDetailsViewModel
            {
                Patient = PatientViewModel.FromEntity(patient),
                Treatments = treatmentsTask.Result.Select(TreatmentViewModel.FromEntity).ToList(),
                Sessions = sessionsTask.Result
                    .OrderByDescending(s => s.FechaHora)
                    .Select(s => SessionViewModel.FromEntityForAdmin(s))
                    .ToList()
            };

            return View(model);
        }

        // GET: /Patients/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient is null)
                return NotFound();

            ViewBag.TreatmentCount = await _treatmentService.CountByPatientIdAsync(id);
            ViewBag.SessionCount = await _sessionService.CountByPatientIdAsync(id);

            return View(PatientViewModel.FromEntity(patient));
        }

        // POST: /Patients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _patientService.DeleteAsync(id);
                TempData["Success"] = "Paciente eliminado correctamente.";
            }
            catch (BusinessValidationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
