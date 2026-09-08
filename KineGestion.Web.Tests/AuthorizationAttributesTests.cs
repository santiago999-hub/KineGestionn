using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KineGestion.Web.Tests
{
    public class AuthorizationAttributesTests
    {
        [Theory]
        [InlineData(typeof(PatientsController), "Admin,Kinesiologo,Asistente")]
        [InlineData(typeof(ProfessionalsController), "Admin")]
        [InlineData(typeof(TreatmentsController), "Admin")]
        [InlineData(typeof(OfficesController), "Admin")]
        [InlineData(typeof(EquipmentsController), "Admin")]
        [InlineData(typeof(AuditController), "Admin")]
        [InlineData(typeof(HomeController), "Admin,Kinesiologo,Asistente")]
        [InlineData(typeof(SessionsController), "Admin,Kinesiologo,Asistente")]
        public void Controller_ShouldHaveExpectedRoles(Type controllerType, string expectedRoles)
        {
            var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(expectedRoles, attr!.Roles);
        }

        [Fact]
        public void Patients_WriteActions_ShouldBeAdminOnly()
        {
            var createGet = typeof(PatientsController).GetMethod("Create", Type.EmptyTypes);
            var createPost = typeof(PatientsController).GetMethod("Create", new[] { typeof(KineGestion.Web.Models.ViewModels.PatientViewModel) });
            var deleteGet = typeof(PatientsController).GetMethod("Delete", new[] { typeof(int) });
            var deletePost = typeof(PatientsController).GetMethod("DeleteConfirmed", new[] { typeof(int) });

            AssertAuthorizeAdminOnly(createGet);
            AssertAuthorizeAdminOnly(createPost);
            AssertAuthorizeAdminOnly(deleteGet);
            AssertAuthorizeAdminOnly(deletePost);
        }

        [Theory]
        [InlineData("Asistente")]
        [InlineData("")]
        [InlineData(null)]
        public async Task Sessions_Create_ShouldRedirectToIndex_WhenUserCannotManageSessions(string? role)
        {
            var controller = new SessionsController(
                Mock.Of<ISessionService>(),
                Mock.Of<IPatientService>(),
                Mock.Of<IProfessionalService>(),
                Mock.Of<ITreatmentService>(),
                Mock.Of<IOfficeService>());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildPrincipal(role)
                }
            };
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.HttpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            var result = await controller.Create();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SessionsController.Index), redirect.ActionName);
        }

        private static ClaimsPrincipal BuildPrincipal(string? role)
        {
            var claims = new System.Collections.Generic.List<Claim>();
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private static void AssertAuthorizeAdminOnly(MethodInfo? method)
        {
            Assert.NotNull(method);
            var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attr);
            Assert.Equal("Admin", attr!.Roles);
        }

        [Fact]
        public void Sessions_DeleteActions_ShouldBeAdminOnly()
        {
            var deleteGet = typeof(SessionsController).GetMethod("Delete", new[] { typeof(int) });
            var deletePost = typeof(SessionsController).GetMethod("DeleteConfirmed", new[] { typeof(int) });

            Assert.NotNull(deleteGet);
            Assert.NotNull(deletePost);

            var deleteGetAttr = deleteGet!.GetCustomAttribute<AuthorizeAttribute>();
            var deletePostAttr = deletePost!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(deleteGetAttr);
            Assert.NotNull(deletePostAttr);
            Assert.Equal("Admin", deleteGetAttr!.Roles);
            Assert.Equal("Admin", deletePostAttr!.Roles);
        }
    }
}