using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class OfficesController : Controller
    {
        private readonly IOfficeService _officeService;

        public OfficesController(IOfficeService officeService)
        {
            _officeService = officeService;
        }

        // GET: /Offices
        public async Task<IActionResult> Index()
        {
            var offices = await _officeService.GetAllAsync();
            var viewModels = System.Linq.Enumerable.Select(offices, OfficeViewModel.FromEntity);
            return View(viewModels);
        }

        // GET: /Offices/Create
        public IActionResult Create()
            => View(new OfficeViewModel { IsActive = true });

        // POST: /Offices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OfficeViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View(viewModel);

            await _officeService.CreateAsync(viewModel.ToEntity());
            TempData["Success"] = "Consultorio creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Offices/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var office = await _officeService.GetByIdAsync(id);
            if (office is null)
                return NotFound();

            return View(OfficeViewModel.FromEntity(office));
        }

        // POST: /Offices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OfficeViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(viewModel);

            await _officeService.UpdateAsync(viewModel.ToEntity());
            TempData["Success"] = "Consultorio actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Offices/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var office = await _officeService.GetByIdAsync(id);
            if (office is null)
                return NotFound();

            return View(OfficeViewModel.FromEntity(office));
        }

        // GET: /Offices/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var office = await _officeService.GetByIdAsync(id);
            if (office is null)
                return NotFound();

            return View(OfficeViewModel.FromEntity(office));
        }

        // POST: /Offices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _officeService.DeleteAsync(id);
            TempData["Success"] = "Consultorio eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
