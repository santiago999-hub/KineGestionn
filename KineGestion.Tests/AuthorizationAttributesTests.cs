using System;
using System.Reflection;
using KineGestion.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace KineGestion.Tests
{
    public class AuthorizationAttributesTests
    {
        [Theory]
        [InlineData(typeof(PatientsController), "Admin")]
        [InlineData(typeof(ProfessionalsController), "Admin")]
        [InlineData(typeof(TreatmentsController), "Admin")]
        [InlineData(typeof(OfficesController), "Admin")]
        [InlineData(typeof(HomeController), "Admin,Kinesiologo")]
        [InlineData(typeof(SessionsController), "Admin,Kinesiologo")]
        public void Controller_ShouldHaveExpectedRoles(Type controllerType, string expectedRoles)
        {
            var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(expectedRoles, attr!.Roles);
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
