using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProfessionalsController : Controller
    {
        private readonly IProfessionalService _professionalService;

        public ProfessionalsController(IProfessionalService professionalService)
        {
            _professionalService = professionalService;
        }

        public async Task<IActionResult> Index()
        {
            var professionals = await _professionalService.GetActiveProfessionalsAsync();
            var viewModels = professionals.Select(ProfessionalViewModel.FromEntity);
            return View(viewModels);
        }

        public IActionResult Create()
        {
            return View(new ProfessionalViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProfessionalViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View(viewModel);

            try
            {
                var professional = viewModel.ToEntity();
                await _professionalService.CreateAsync(professional);
                TempData["Success"] = $"Profesional {viewModel.Nombre} {viewModel.Apellido} registrado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? nameof(viewModel.Matricula) : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var professional = await _professionalService.GetByIdAsync(id);
            if (professional is null)
                return NotFound();

            return View(ProfessionalViewModel.FromEntity(professional));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProfessionalViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(viewModel);

            try
            {
                var professional = viewModel.ToEntity();
                await _professionalService.UpdateAsync(professional);
                TempData["Success"] = "Profesional actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? nameof(viewModel.Matricula) : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var professional = await _professionalService.GetByIdAsync(id);
            if (professional is null)
                return NotFound();

            return View(ProfessionalViewModel.FromEntity(professional));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var professional = await _professionalService.GetByIdAsync(id);
            if (professional is null)
                return NotFound();

            return View(ProfessionalViewModel.FromEntity(professional));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _professionalService.DeleteAsync(id);
            TempData["Success"] = "Profesional dado de baja correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
