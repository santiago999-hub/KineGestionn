using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KineGestion.Data.Context;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Web.Services
{
    /// <summary>
    /// Implementación concreta de IIdentityService.
    /// Vive en la capa Web porque Identity es un servicio de infraestructura de presentación.
    /// Concentra toda la lógica de UserManager/RoleManager en un único lugar (DRY + SRP).
    /// </summary>
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _db;

        public IdentityService(UserManager<IdentityUser> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        /// <inheritdoc/>
        public async Task<(IReadOnlyList<UserListItemViewModel> Items, int TotalCount)> GetPagedUsersAsync(
            string? search, int page, int pageSize)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"));
            }

            int totalCount = await query.CountAsync();

            var paged = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Carga todos los roles de la página en una única consulta SQL: patrón anti N+1.
            // Sin este JOIN, cada usuario requeriría un roundtrip separado a AspNetUserRoles.
            var userIds = paged.Select(u => u.Id).ToList();
            var userRolesList = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, RoleName = r.Name }
            ).ToListAsync();

            var rolesByUser = userRolesList
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First().RoleName ?? "Sin rol");

            IReadOnlyList<UserListItemViewModel> items = paged.Select(user => new UserListItemViewModel
            {
                Id             = user.Id,
                Email          = user.Email ?? string.Empty,
                Rol            = rolesByUser.GetValueOrDefault(user.Id, "Sin rol"),
                EmailConfirmed = user.EmailConfirmed
            }).ToList();

            return (items, totalCount);
        }

        /// <inheritdoc/>
        public async Task<UserViewModel?> GetUserForEditAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null) return null;

            var roles  = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var profClaim = claims.FirstOrDefault(c => c.Type == "ProfessionalId");

            return new UserViewModel
            {
                Id             = user.Id,
                Email          = user.Email ?? string.Empty,
                Rol            = roles.FirstOrDefault() ?? string.Empty,
                ProfessionalId = profClaim is not null && int.TryParse(profClaim.Value, out var pid)
                                    ? pid
                                    : null
            };
        }

        /// <inheritdoc/>
        public async Task<UserListItemViewModel?> GetUserForDeleteAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            return new UserListItemViewModel
            {
                Id    = user.Id,
                Email = user.Email ?? string.Empty,
                Rol   = roles.FirstOrDefault() ?? "Sin rol"
            };
        }

        /// <inheritdoc/>
        public async Task<IdentityOperationResult> CreateUserAsync(UserViewModel viewModel)
        {
            var existing = await _userManager.FindByEmailAsync(viewModel.Email);
            if (existing is not null)
                return IdentityOperationResult.Fail(
                    "Ya existe un usuario registrado con ese email.",
                    nameof(UserViewModel.Email));

            var user = new IdentityUser
            {
                UserName       = viewModel.Email,
                Email          = viewModel.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, viewModel.Password!);
            if (!result.Succeeded)
                return IdentityOperationResult.FromIdentityErrors(result.Errors);

            await _userManager.AddToRoleAsync(user, viewModel.Rol);

            if (viewModel.Rol == "Kinesiologo" && viewModel.ProfessionalId.HasValue)
                await _userManager.AddClaimAsync(
                    user,
                    new Claim("ProfessionalId", viewModel.ProfessionalId.Value.ToString()));

            return IdentityOperationResult.Ok();
        }

        /// <inheritdoc/>
        public async Task<IdentityOperationResult> UpdateUserAsync(string id, UserViewModel viewModel)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return IdentityOperationResult.Fail("Usuario no encontrado.");

            // Actualizar email si cambió
            if (user.Email != viewModel.Email)
            {
                var emailInUse = await _userManager.FindByEmailAsync(viewModel.Email);
                if (emailInUse is not null)
                    return IdentityOperationResult.Fail(
                        "Ya existe otro usuario con ese email.",
                        nameof(UserViewModel.Email));

                user.UserName = viewModel.Email;
                user.Email    = viewModel.Email;
                await _userManager.UpdateAsync(user);
            }

            // Actualizar contraseña si el admin proporcionó una nueva
            if (!string.IsNullOrWhiteSpace(viewModel.Password))
            {
                var token    = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, viewModel.Password);
                if (!pwResult.Succeeded)
                    return IdentityOperationResult.FromIdentityErrors(pwResult.Errors);
            }

            // Reemplazar roles: remover todos y asignar el seleccionado
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, viewModel.Rol);

            // Reemplazar claim ProfessionalId (si existe, remover y re-agregar)
            var existingClaims   = await _userManager.GetClaimsAsync(user);
            var existingProfClaim = existingClaims.FirstOrDefault(c => c.Type == "ProfessionalId");
            if (existingProfClaim is not null)
                await _userManager.RemoveClaimAsync(user, existingProfClaim);

            if (viewModel.Rol == "Kinesiologo" && viewModel.ProfessionalId.HasValue)
                await _userManager.AddClaimAsync(
                    user,
                    new Claim("ProfessionalId", viewModel.ProfessionalId.Value.ToString()));

            return IdentityOperationResult.Ok();
        }

        /// <inheritdoc/>
        public async Task<IdentityOperationResult> DeleteUserAsync(string id, string currentUserId)
        {
            // Protección: el administrador activo no puede eliminarse a sí mismo
            if (id == currentUserId)
                return IdentityOperationResult.Fail("No podés eliminar tu propio usuario.");

            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return IdentityOperationResult.Fail("Usuario no encontrado.");

            await _userManager.DeleteAsync(user);
            return IdentityOperationResult.Ok();
        }
    }
}
