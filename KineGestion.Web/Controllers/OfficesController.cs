using System.Threading.Tasks;
using KineGestion.Core.Exceptions;
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
        private readonly ISessionService _sessionService;

        public OfficesController(IOfficeService officeService, ISessionService sessionService)
        {
            _officeService = officeService;
            _sessionService = sessionService;
        }

        // GET: /Offices
        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (offices, totalCount) = await _officeService.GetPagedAsync(page, pageSize, search);
            var viewModels = System.Linq.Enumerable.Select(offices, OfficeViewModel.FromEntity);

            var model = new OfficeIndexViewModel
            {
                Items = viewModels,
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
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

            try
            {
                await _officeService.CreateAsync(viewModel.ToEntity());
                TempData["Success"] = "Consultorio creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
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

            try
            {
                await _officeService.UpdateAsync(viewModel.ToEntity());
                TempData["Success"] = "Consultorio actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                return View(viewModel);
            }
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

            ViewBag.SessionCount = await _sessionService.CountByOfficeIdAsync(id);

            return View(OfficeViewModel.FromEntity(office));
        }

        // POST: /Offices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _officeService.DeleteAsync(id);
                TempData["Success"] = "Consultorio eliminado correctamente.";
            }
            catch (BusinessValidationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
