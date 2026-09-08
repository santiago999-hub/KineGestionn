using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EquipmentsController : BaseController
    {
        private readonly IEquipmentService _equipmentService;
        private readonly IOfficeService _officeService;

        public EquipmentsController(IEquipmentService equipmentService, IOfficeService officeService)
        {
            _equipmentService = equipmentService;
            _officeService = officeService;
        }

        public async Task<IActionResult> Index(string? search, int? officeId, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (items, totalCount) = await _equipmentService.GetPagedAsync(page, pageSize, search, officeId);
            var viewModels = items.Select(EquipmentViewModel.FromEntity).ToList();
            var offices = await LoadOfficesSelectListAsync(officeId);

            var model = new EquipmentIndexViewModel
            {
                Items = viewModels,
                Search = search,
                OfficeId = officeId,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Offices = offices
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var equipment = await _equipmentService.GetByIdAsync(id);
            if (equipment is null)
                return NotFound();

            return View(EquipmentViewModel.FromEntity(equipment));
        }

        public async Task<IActionResult> Create()
        {
            var viewModel = new EquipmentViewModel();
            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var entity = viewModel.ToEntity();
                await _equipmentService.CreateAsync(entity);
                TempData["Success"] = $"Equipamiento '{viewModel.Name}' creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                AddModelStateError(ex);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var equipment = await _equipmentService.GetByIdAsync(id);
            if (equipment is null)
                return NotFound();

            var viewModel = EquipmentViewModel.FromEntity(equipment);
            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EquipmentViewModel viewModel)
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
                var entity = viewModel.ToEntity();
                await _equipmentService.UpdateAsync(entity);
                TempData["Success"] = "Equipamiento actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                AddModelStateError(ex);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            var equipment = await _equipmentService.GetByIdAsync(id);
            if (equipment is null)
                return NotFound();

            return View(EquipmentViewModel.FromEntity(equipment));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _equipmentService.DeleteAsync(id);
                TempData["Success"] = "Equipamiento eliminado correctamente.";
            }
            catch (BusinessValidationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSelectListsAsync(EquipmentViewModel viewModel)
        {
            var offices = await _officeService.GetActiveAsync();
            viewModel.Offices = offices
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name,
                    Selected = o.Id == viewModel.OfficeId
                })
                .ToList();
        }

        private async Task<IEnumerable<SelectListItem>> LoadOfficesSelectListAsync(int? selectedId)
        {
            var offices = await _officeService.GetActiveAsync();
            var list = offices
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name,
                    Selected = o.Id == selectedId
                })
                .ToList();

            list.Insert(0, new SelectListItem { Value = "", Text = "Todos los consultorios" });
            return list;
        }
    }
}