using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Web.Models.ViewModels;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace KineGestion.Web.Tests
{
    /// <summary>
    /// Tests unitarios para IdentityService.
    /// Se testean los métodos que dependen solo de UserManager (sin AppDbContext).
    /// GetPagedUsersAsync requiere JOIN sobre tablas de Identity → cubre AuthorizationAttributesTests.
    /// </summary>
    public class IdentityServiceTests
    {
        private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
        private readonly IdentityService _service;

        public IdentityServiceTests()
        {
            var store = new Mock<IUserStore<IdentityUser>>();
#pragma warning disable CS8625 // Patrón estándar para mockear UserManager: sus parámetros opcionales son non-nullable pero se aceptan null en tests.
            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

            // AppDbContext se pasa null porque ninguno de los métodos testeados aquí accede a _db.
            _service = new IdentityService(_userManagerMock.Object, null!);
        }

        // ─── CreateUserAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_ShouldReturnFail_WhenEmailAlreadyExists()
        {
            var vm = new UserViewModel { Email = "existente@test.com", Password = "Pass1234!", Rol = "Admin" };
            _userManagerMock
                .Setup(m => m.FindByEmailAsync(vm.Email))
                .ReturnsAsync(new IdentityUser { Email = vm.Email });

            var result = await _service.CreateUserAsync(vm);

            Assert.False(result.Succeeded);
            Assert.Contains("Ya existe un usuario registrado con ese email.", result.Errors);
            Assert.Equal(nameof(UserViewModel.Email), result.ConflictingField);
        }

        [Fact]
        public async Task CreateUserAsync_ShouldReturnFail_WhenIdentityCreationFails()
        {
            var vm = new UserViewModel { Email = "nuevo@test.com", Password = "débil", Rol = "Admin" };
            _userManagerMock
                .Setup(m => m.FindByEmailAsync(vm.Email))
                .ReturnsAsync((IdentityUser?)null);
            _userManagerMock
                .Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), vm.Password))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "La contraseña es demasiado corta." }));

            var result = await _service.CreateUserAsync(vm);

            Assert.False(result.Succeeded);
            Assert.Contains("La contraseña es demasiado corta.", result.Errors);
        }

        [Fact]
        public async Task CreateUserAsync_ShouldReturnOk_WhenDataIsValid()
        {
            var vm = new UserViewModel { Email = "valido@test.com", Password = "Pass1234!", Rol = "Admin" };
            _userManagerMock
                .Setup(m => m.FindByEmailAsync(vm.Email))
                .ReturnsAsync((IdentityUser?)null);
            _userManagerMock
                .Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), vm.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock
                .Setup(m => m.AddToRoleAsync(It.IsAny<IdentityUser>(), vm.Rol))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _service.CreateUserAsync(vm);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task CreateUserAsync_ShouldReturnFail_WhenKinesiologoHasNoProfessionalId()
        {
            var vm = new UserViewModel { Email = "kine-sin-prof@test.com", Password = "Pass1234!", Rol = "Kinesiologo", ProfessionalId = null };

            var result = await _service.CreateUserAsync(vm);

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(UserViewModel.ProfessionalId), result.ConflictingField);
            Assert.Contains(result.Errors, error => error.Contains("Debe seleccionar un profesional asociado"));
            _userManagerMock.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_ShouldReturnFail_WhenRoleAssignmentFails()
        {
            var vm = new UserViewModel { Email = "fallo-rol@test.com", Password = "Pass1234!", Rol = "Admin" };
            var createdUser = new IdentityUser { Id = "u-create", Email = vm.Email, UserName = vm.Email };

            _userManagerMock
                .Setup(m => m.FindByEmailAsync(vm.Email))
                .ReturnsAsync((IdentityUser?)null);
            _userManagerMock
                .Setup(m => m.CreateAsync(It.IsAny<IdentityUser>(), vm.Password))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<IdentityUser, string>((u, _) =>
                {
                    createdUser = u;
                });
            _userManagerMock
                .Setup(m => m.AddToRoleAsync(It.IsAny<IdentityUser>(), vm.Rol))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Rol inválido." }));
            _userManagerMock
                .Setup(m => m.DeleteAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _service.CreateUserAsync(vm);

            Assert.False(result.Succeeded);
            Assert.Contains("Rol inválido.", result.Errors);
            _userManagerMock.Verify(m => m.DeleteAsync(createdUser), Times.Once);
        }

        // ─── DeleteUserAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteUserAsync_ShouldReturnFail_WhenDeletingSelf()
        {
            var result = await _service.DeleteUserAsync("admin-1", "admin-1");

            Assert.False(result.Succeeded);
            Assert.Contains("No podés eliminar tu propio usuario.", result.Errors);
            // UserManager no debe consultarse si el id coincide
            _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldReturnFail_WhenUserNotFound()
        {
            _userManagerMock
                .Setup(m => m.FindByIdAsync("inexistente"))
                .ReturnsAsync((IdentityUser?)null);

            var result = await _service.DeleteUserAsync("inexistente", "admin-1");

            Assert.False(result.Succeeded);
            Assert.Contains("Usuario no encontrado.", result.Errors);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldCallDelete_WhenUserExists()
        {
            var user = new IdentityUser { Id = "user-99", Email = "borrar@test.com" };
            _userManagerMock.Setup(m => m.FindByIdAsync("user-99")).ReturnsAsync(user);
            _userManagerMock.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

            var result = await _service.DeleteUserAsync("user-99", "admin-1");

            Assert.True(result.Succeeded);
            _userManagerMock.Verify(m => m.DeleteAsync(user), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldReturnFail_WhenDeleteFails()
        {
            var user = new IdentityUser { Id = "user-88", Email = "nodelete@test.com" };
            _userManagerMock.Setup(m => m.FindByIdAsync("user-88")).ReturnsAsync(user);
            _userManagerMock.Setup(m => m.DeleteAsync(user))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error al borrar." }));

            var result = await _service.DeleteUserAsync("user-88", "admin-1");

            Assert.False(result.Succeeded);
            Assert.Contains("Error al borrar.", result.Errors);
        }

        // ─── UpdateUserAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFail_WhenUserNotFound()
        {
            _userManagerMock
                .Setup(m => m.FindByIdAsync("missing"))
                .ReturnsAsync((IdentityUser?)null);

            var result = await _service.UpdateUserAsync("missing", new UserViewModel());

            Assert.False(result.Succeeded);
            Assert.Contains("Usuario no encontrado.", result.Errors);
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFail_WhenNewEmailAlreadyInUse()
        {
            var current  = new IdentityUser { Id = "u1", Email = "actual@test.com" };
            var conflict = new IdentityUser { Id = "u2", Email = "ocupado@test.com" };

            _userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(current);
            _userManagerMock.Setup(m => m.FindByEmailAsync("ocupado@test.com")).ReturnsAsync(conflict);

            var vm = new UserViewModel { Email = "ocupado@test.com", Rol = "Admin" };
            var result = await _service.UpdateUserAsync("u1", vm);

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(UserViewModel.Email), result.ConflictingField);
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFail_WhenKinesiologoHasNoProfessionalId()
        {
            var vm = new UserViewModel { Email = "kine-edit@test.com", Rol = "Kinesiologo", ProfessionalId = null };

            var result = await _service.UpdateUserAsync("u1", vm);

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(UserViewModel.ProfessionalId), result.ConflictingField);
            Assert.Contains(result.Errors, error => error.Contains("Debe seleccionar un profesional asociado"));
            _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        // ─── GetUserForDeleteAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetUserForDeleteAsync_ShouldReturnNull_WhenUserNotFound()
        {
            _userManagerMock
                .Setup(m => m.FindByIdAsync("bad-id"))
                .ReturnsAsync((IdentityUser?)null);

            var result = await _service.GetUserForDeleteAsync("bad-id");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserForDeleteAsync_ShouldReturnViewModel_WhenUserExists()
        {
            var user = new IdentityUser { Id = "u1", Email = "kine@test.com" };
            _userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
            _userManagerMock
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Kinesiologo" });

            var result = await _service.GetUserForDeleteAsync("u1");

            Assert.NotNull(result);
            Assert.Equal("kine@test.com", result!.Email);
            Assert.Equal("Kinesiologo", result.Rol);
        }

        // ─── GetUserForEditAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetUserForEditAsync_ShouldReturnNull_WhenUserNotFound()
        {
            _userManagerMock
                .Setup(m => m.FindByIdAsync("ghost"))
                .ReturnsAsync((IdentityUser?)null);

            var result = await _service.GetUserForEditAsync("ghost");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserForEditAsync_ShouldReturnViewModel_WithRolAndProfessionalId()
        {
            var user = new IdentityUser { Id = "u5", Email = "kine5@test.com" };
            _userManagerMock.Setup(m => m.FindByIdAsync("u5")).ReturnsAsync(user);
            _userManagerMock
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Kinesiologo" });
            _userManagerMock
                .Setup(m => m.GetClaimsAsync(user))
                .ReturnsAsync(new List<System.Security.Claims.Claim>
                {
                    new("ProfessionalId", "42")
                });

            var result = await _service.GetUserForEditAsync("u5");

            Assert.NotNull(result);
            Assert.Equal("kine5@test.com", result!.Email);
            Assert.Equal("Kinesiologo", result.Rol);
            Assert.Equal(42, result.ProfessionalId);
        }
    }
}
