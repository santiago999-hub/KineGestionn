using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TreatmentsController : Controller
    {
        private readonly ITreatmentService _treatmentService;
        private readonly IPatientService _patientService;

        public TreatmentsController(ITreatmentService treatmentService, IPatientService patientService)
        {
            _treatmentService = treatmentService;
            _patientService = patientService;
        }

        // GET: /Treatments
        public async Task<IActionResult> Index()
        {
            var treatments = await _treatmentService.GetAllAsync();
            var viewModels = treatments.Select(TreatmentViewModel.FromEntity);
            return View(viewModels);
        }

        // GET: /Treatments/Create
        public async Task<IActionResult> Create(int? patientId = null)
        {
            var viewModel = new TreatmentViewModel
            {
                FechaInicio = DateTime.Today,
                PacienteId = patientId ?? 0
            };
            await LoadPatientsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Treatments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TreatmentViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadPatientsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var treatment = viewModel.ToEntity();
                await _treatmentService.CreateAsync(treatment);
                TempData["Success"] = $"Tratamiento '{viewModel.Descripcion}' registrado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                await LoadPatientsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /Treatments/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var treatment = await _treatmentService.GetByIdAsync(id);
            if (treatment is null)
                return NotFound();

            var viewModel = TreatmentViewModel.FromEntity(treatment);
            await LoadPatientsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Treatments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TreatmentViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadPatientsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var treatment = viewModel.ToEntity();
                await _treatmentService.UpdateAsync(treatment);
                TempData["Success"] = "Tratamiento actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                await LoadPatientsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /Treatments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var treatment = await _treatmentService.GetByIdAsync(id);
            if (treatment is null)
                return NotFound();

            return View(TreatmentViewModel.FromEntity(treatment));
        }

        // GET: /Treatments/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var treatment = await _treatmentService.GetByIdAsync(id);
            if (treatment is null)
                return NotFound();

            return View(TreatmentViewModel.FromEntity(treatment));
        }

        // POST: /Treatments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _treatmentService.DeleteAsync(id);
            TempData["Success"] = "Tratamiento eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadPatientsAsync(TreatmentViewModel viewModel)
        {
            var patients = await _patientService.GetActivePatientsAsync();
            viewModel.Pacientes = patients
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} — DNI {p.DNI}"
                })
                .OrderBy(p => p.Text)
                .ToList();
        }
    }
}
