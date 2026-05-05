using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace KineGestion.Web.Services
{
    /// <summary>
    /// Abstracción de las operaciones de gestión de usuarios e identidad (Identity).
    /// Aplicación del Principio de Responsabilidad Única (SOLID-S):
    /// el controlador solo maneja el flujo HTTP; toda la lógica de Identity vive aquí.
    /// </summary>
    public interface IIdentityService
    {
        /// <summary>
        /// Obtiene usuarios paginados con sus roles en una sola consulta SQL (patrón anti N+1).
        /// </summary>
        Task<(IReadOnlyList<UserListItemViewModel> Items, int TotalCount)> GetPagedUsersAsync(
            string? search, int page, int pageSize);

        /// <summary>Obtiene los datos de un usuario para la vista de edición.</summary>
        Task<UserViewModel?> GetUserForEditAsync(string id);

        /// <summary>Obtiene los datos mínimos de un usuario para la confirmación de borrado.</summary>
        Task<UserListItemViewModel?> GetUserForDeleteAsync(string id);

        /// <summary>Crea un usuario con el rol y claim de profesional indicados.</summary>
        Task<IdentityOperationResult> CreateUserAsync(UserViewModel viewModel);

        /// <summary>Actualiza email, contraseña, rol y claim ProfessionalId de un usuario.</summary>
        Task<IdentityOperationResult> UpdateUserAsync(string id, UserViewModel viewModel);

        /// <summary>
        /// Elimina un usuario. Impide la auto-eliminación del administrador activo.
        /// </summary>
        /// <param name="id">Id del usuario a eliminar.</param>
        /// <param name="currentUserId">Id del usuario autenticado (para protección de auto-eliminación).</param>
        Task<IdentityOperationResult> DeleteUserAsync(string id, string currentUserId);
    }

    /// <summary>
    /// Resultado de una operación de Identity.
    /// Encapsula éxito, mensajes de error y el campo afectado (si corresponde),
    /// permitiendo que el controlador agregue errores al ModelState sin conocer los detalles internos.
    /// </summary>
    public record IdentityOperationResult(
        bool Succeeded,
        IReadOnlyList<string> Errors,
        string? ConflictingField = null)
    {
        public static IdentityOperationResult Ok() =>
            new(true, Array.Empty<string>());

        public static IdentityOperationResult Fail(string error, string? field = null) =>
            new(false, new[] { error }, field);

        public static IdentityOperationResult FromIdentityErrors(
            IEnumerable<IdentityError> errors, string? field = null) =>
            new(false, errors.Select(e => e.Description).ToList(), field);
    }
}
