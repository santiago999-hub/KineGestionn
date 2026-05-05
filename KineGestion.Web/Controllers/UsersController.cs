using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        // IIdentityService encapsula toda la lógica de UserManager/Identity (Principio SRP).
        // El controlador solo orquesta el flujo HTTP: valida, delega y responde.
        private readonly IIdentityService _identityService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IProfessionalService _professionalService;

        public UsersController(
            IIdentityService identityService,
            RoleManager<IdentityRole> roleManager,
            IProfessionalService professionalService)
        {
            _identityService      = identityService;
            _roleManager          = roleManager;
            _professionalService  = professionalService;
        }

        // GET: /Users
        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var (items, totalCount) = await _identityService.GetPagedUsersAsync(search, page, pageSize);

            var model = new UserIndexViewModel
            {
                Items      = items,
                Search     = search,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        // GET: /Users/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new UserViewModel();
            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.Password))
                ModelState.AddModelError(nameof(viewModel.Password), "La contraseña es obligatoria al crear un usuario.");

            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            var result = await _identityService.CreateUserAsync(viewModel);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(result.ConflictingField ?? string.Empty, error);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            TempData["Success"] = $"Usuario {viewModel.Email} creado correctamente con el rol {viewModel.Rol}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var viewModel = await _identityService.GetUserForEditAsync(id);
            if (viewModel is null)
                return NotFound();

            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Users/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            // En edición la contraseña es opcional
            if (string.IsNullOrWhiteSpace(viewModel.Password))
            {
                ModelState.Remove(nameof(viewModel.Password));
                ModelState.Remove(nameof(viewModel.ConfirmPassword));
            }

            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            var result = await _identityService.UpdateUserAsync(id, viewModel);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(result.ConflictingField ?? string.Empty, error);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            TempData["Success"] = $"Usuario {viewModel.Email} actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var viewModel = await _identityService.GetUserForDeleteAsync(id);
            if (viewModel is null)
                return NotFound();

            return View(viewModel);
        }

        // POST: /Users/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            // El ID del usuario autenticado se obtiene de los claims: no necesita UserManager.
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _identityService.DeleteUserAsync(id, currentUserId);
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Errors.FirstOrDefault() ?? "No se pudo eliminar el usuario.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Usuario eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private async Task LoadSelectListsAsync(UserViewModel viewModel)
        {
            viewModel.Roles = _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Name,
                    Text = r.Name,
                    Selected = r.Name == viewModel.Rol
                })
                .ToList();

            // GetForSelectAsync: proyección SQL mínima (solo Id, Nombre, Apellido, Matricula).
            // Evita cargar todos los campos de Professional solo para un dropdown.
            // El ORDER BY se hace en SQL, no en memoria.
            var professionals = await _professionalService.GetForSelectAsync();
            viewModel.Profesionales = professionals
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} — Matr. {p.Matricula}",
                    Selected = p.Id == viewModel.ProfessionalId
                })
                .ToList();
        }
    }
}
