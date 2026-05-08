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
            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize switch
            {
                < 5 => 10,
                > 100 => 100,
                _ => pageSize
            };

            var query = _userManager.Users
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"));
            }

            int totalCount = await query.CountAsync();

            var paged = await query
                .OrderBy(u => u.Email)
                .ThenBy(u => u.Id)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.EmailConfirmed
                })
                .ToListAsync();

            if (paged.Count == 0)
                return (new List<UserListItemViewModel>(), totalCount);

            // Carga todos los roles de la página en una única consulta SQL: patrón anti N+1.
            // Sin este JOIN, cada usuario requeriría un roundtrip separado a AspNetUserRoles.
            var userIds = paged.Select(u => u.Id).ToList();
            var userRolesList = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, RoleName = r.Name }
            )
            .AsNoTracking()
            .ToListAsync();

            var rolesByUser = userRolesList
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(x => x.RoleName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Sin rol");

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
            var roleValidation = ValidateRoleProfessionalLink(viewModel);
            if (roleValidation is not null)
                return roleValidation;

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

            var roleResult = await _userManager.AddToRoleAsync(user, viewModel.Rol);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return IdentityOperationResult.FromIdentityErrors(roleResult.Errors);
            }

            if (viewModel.Rol == "Kinesiologo")
            {
                var claimResult = await _userManager.AddClaimAsync(
                    user,
                    new Claim("ProfessionalId", viewModel.ProfessionalId!.Value.ToString()));

                if (!claimResult.Succeeded)
                {
                    await _userManager.RemoveFromRoleAsync(user, viewModel.Rol);
                    await _userManager.DeleteAsync(user);
                    return IdentityOperationResult.FromIdentityErrors(claimResult.Errors);
                }
            }

            return IdentityOperationResult.Ok();
        }

        /// <inheritdoc/>
        public async Task<IdentityOperationResult> UpdateUserAsync(string id, UserViewModel viewModel)
        {
            var roleValidation = ValidateRoleProfessionalLink(viewModel);
            if (roleValidation is not null)
                return roleValidation;

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
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                    return IdentityOperationResult.FromIdentityErrors(updateResult.Errors, nameof(UserViewModel.Email));
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
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeRolesResult.Succeeded)
                return IdentityOperationResult.FromIdentityErrors(removeRolesResult.Errors);

            var addRoleResult = await _userManager.AddToRoleAsync(user, viewModel.Rol);
            if (!addRoleResult.Succeeded)
                return IdentityOperationResult.FromIdentityErrors(addRoleResult.Errors);

            // Reemplazar claim ProfessionalId (si existe, remover y re-agregar)
            var existingClaims   = await _userManager.GetClaimsAsync(user);
            var existingProfClaim = existingClaims.FirstOrDefault(c => c.Type == "ProfessionalId");
            if (existingProfClaim is not null)
            {
                var removeClaimResult = await _userManager.RemoveClaimAsync(user, existingProfClaim);
                if (!removeClaimResult.Succeeded)
                    return IdentityOperationResult.FromIdentityErrors(removeClaimResult.Errors);
            }

            if (viewModel.Rol == "Kinesiologo")
            {
                var addClaimResult = await _userManager.AddClaimAsync(
                    user,
                    new Claim("ProfessionalId", viewModel.ProfessionalId!.Value.ToString()));

                if (!addClaimResult.Succeeded)
                    return IdentityOperationResult.FromIdentityErrors(addClaimResult.Errors);
            }

            return IdentityOperationResult.Ok();
        }

        private static IdentityOperationResult? ValidateRoleProfessionalLink(UserViewModel viewModel)
        {
            if (viewModel.Rol == "Kinesiologo" && !viewModel.ProfessionalId.HasValue)
            {
                return IdentityOperationResult.Fail(
                    "Debe seleccionar un profesional asociado para el rol Kinesiólogo.",
                    nameof(UserViewModel.ProfessionalId));
            }

            return null;
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

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
                return IdentityOperationResult.FromIdentityErrors(deleteResult.Errors);

            return IdentityOperationResult.Ok();
        }
    }
}
