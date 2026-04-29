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
        private readonly ISessionService _sessionService;

        public ProfessionalsController(IProfessionalService professionalService, ISessionService sessionService)
        {
            _professionalService = professionalService;
            _sessionService = sessionService;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (professionals, totalCount) = await _professionalService.GetPagedAsync(page, pageSize, search);
            var viewModels = professionals.Select(ProfessionalViewModel.FromEntity).ToList();

            var model = new ProfessionalIndexViewModel
            {
                Items = viewModels,
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
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

            ViewBag.SessionCount = await _sessionService.CountByProfessionalIdAsync(id);

            return View(ProfessionalViewModel.FromEntity(professional));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _professionalService.DeleteAsync(id);
                TempData["Success"] = "Profesional dado de baja correctamente.";
            }
            catch (BusinessValidationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
