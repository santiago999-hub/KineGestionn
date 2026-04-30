using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IProfessionalService _professionalService;

        public UsersController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IProfessionalService professionalService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _professionalService = professionalService;
        }

        // GET: /Users
        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            var allUsers = _userManager.Users.ToList();

            if (!string.IsNullOrWhiteSpace(search))
                allUsers = allUsers.Where(u => u.Email != null &&
                    u.Email.Contains(search, System.StringComparison.OrdinalIgnoreCase)).ToList();

            var totalCount = allUsers.Count;
            var paged = allUsers.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var items = new List<UserListItemViewModel>();
            foreach (var user in paged)
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    Rol = roles.FirstOrDefault() ?? "Sin rol",
                    EmailConfirmed = user.EmailConfirmed
                });
            }

            var model = new UserIndexViewModel
            {
                Items = items,
                Search = search,
                Page = page,
                PageSize = pageSize,
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

            var existing = await _userManager.FindByEmailAsync(viewModel.Email);
            if (existing is not null)
            {
                ModelState.AddModelError(nameof(viewModel.Email), "Ya existe un usuario registrado con ese email.");
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            var user = new IdentityUser
            {
                UserName = viewModel.Email,
                Email = viewModel.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, viewModel.Password!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            await _userManager.AddToRoleAsync(user, viewModel.Rol);

            if (viewModel.Rol == "Kinesiologo" && viewModel.ProfessionalId.HasValue)
                await _userManager.AddClaimAsync(user, new Claim("ProfessionalId", viewModel.ProfessionalId.Value.ToString()));

            TempData["Success"] = $"Usuario {viewModel.Email} creado correctamente con el rol {viewModel.Rol}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var profClaim = claims.FirstOrDefault(c => c.Type == "ProfessionalId");

            var viewModel = new UserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Rol = roles.FirstOrDefault() ?? string.Empty,
                ProfessionalId = profClaim != null && int.TryParse(profClaim.Value, out var pid) ? pid : null
            };
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

            // En edición la contraseña es opcional, limpiar errores si está vacía
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

            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            // Actualizar email si cambió
            if (user.Email != viewModel.Email)
            {
                var emailInUse = await _userManager.FindByEmailAsync(viewModel.Email);
                if (emailInUse is not null)
                {
                    ModelState.AddModelError(nameof(viewModel.Email), "Ya existe otro usuario con ese email.");
                    await LoadSelectListsAsync(viewModel);
                    return View(viewModel);
                }
                user.UserName = viewModel.Email;
                user.Email = viewModel.Email;
                await _userManager.UpdateAsync(user);
            }

            // Actualizar contraseña si se ingresó
            if (!string.IsNullOrWhiteSpace(viewModel.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, viewModel.Password);
                if (!pwResult.Succeeded)
                {
                    foreach (var error in pwResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    await LoadSelectListsAsync(viewModel);
                    return View(viewModel);
                }
            }

            // Actualizar rol
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, viewModel.Rol);

            // Actualizar claim ProfessionalId
            var existingClaims = await _userManager.GetClaimsAsync(user);
            var existingProfClaim = existingClaims.FirstOrDefault(c => c.Type == "ProfessionalId");
            if (existingProfClaim is not null)
                await _userManager.RemoveClaimAsync(user, existingProfClaim);

            if (viewModel.Rol == "Kinesiologo" && viewModel.ProfessionalId.HasValue)
                await _userManager.AddClaimAsync(user, new Claim("ProfessionalId", viewModel.ProfessionalId.Value.ToString()));

            TempData["Success"] = $"Usuario {viewModel.Email} actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var viewModel = new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Rol = roles.FirstOrDefault() ?? "Sin rol"
            };
            return View(viewModel);
        }

        // POST: /Users/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            // No permitir que el admin se elimine a sí mismo
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == id)
            {
                TempData["Error"] = "No podés eliminar tu propio usuario.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            await _userManager.DeleteAsync(user);
            TempData["Success"] = $"Usuario {user.Email} eliminado correctamente.";
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

            var professionals = await _professionalService.GetActiveProfessionalsAsync();
            viewModel.Profesionales = professionals
                .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre)
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
