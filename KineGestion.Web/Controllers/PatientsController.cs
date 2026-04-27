using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Controllers
{
    public class PatientsController : Controller
    {
        private readonly IPatientService _patientService;

        // ASP.NET Core inyecta automáticamente IPatientService
        // gracias al registro AddScoped en Program.cs.
        public PatientsController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        // GET: /Patients
        public async Task<IActionResult> Index()
        {
            var patients = await _patientService.GetActivePatientsAsync();
            var viewModels = patients.Select(PatientViewModel.FromEntity);
            return View(viewModels);
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
            catch (InvalidOperationException ex)
            {
                // Captura la excepción de negocio (DNI duplicado) y la muestra
                // como error del campo DNI en el formulario — sin pantalla de error.
                ModelState.AddModelError(nameof(viewModel.DNI), ex.Message);
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
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(viewModel.DNI), ex.Message);
                return View(viewModel);
            }
        }

        // GET: /Patients/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient is null)
                return NotFound();

            return View(PatientViewModel.FromEntity(patient));
        }

        // GET: /Patients/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient is null)
                return NotFound();

            return View(PatientViewModel.FromEntity(patient));
        }

        // POST: /Patients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _patientService.DeleteAsync(id);
            TempData["Success"] = "Paciente eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
