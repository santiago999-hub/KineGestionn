using KineGestion.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KineGestion.Web.Tests
{
    public class LocalizationControllerTests
    {
        [Fact]
        public void SetLanguage_ShouldSetCultureCookie_AndRedirectLocalUrl()
        {
            var controller = BuildController(isLocalUrl: true);

            var result = controller.SetLanguage("en", "/Audit/Index");

            var redirect = Assert.IsType<LocalRedirectResult>(result);
            Assert.Equal("/Audit/Index", redirect.Url);

            var setCookie = controller.Response.Headers.SetCookie.ToString();
            Assert.Contains(CookieRequestCultureProvider.DefaultCookieName, setCookie, StringComparison.Ordinal);
            Assert.Contains("c%3Den%7Cuic%3Den", setCookie, StringComparison.Ordinal);
        }

        [Fact]
        public void SetLanguage_ShouldFallbackToSpanish_WhenCultureIsInvalid()
        {
            var controller = BuildController(isLocalUrl: true);

            var result = controller.SetLanguage("pt", "/Home/Index");

            var redirect = Assert.IsType<LocalRedirectResult>(result);
            Assert.Equal("/Home/Index", redirect.Url);

            var setCookie = controller.Response.Headers.SetCookie.ToString();
            Assert.Contains("c%3Des%7Cuic%3Des", setCookie, StringComparison.Ordinal);
        }

        [Fact]
        public void SetLanguage_ShouldRedirectHome_WhenReturnUrlIsNotLocal()
        {
            var controller = BuildController(isLocalUrl: false);

            var result = controller.SetLanguage("es", "https://malicious.test");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }

        private static LocalizationController BuildController(bool isLocalUrl)
        {
            var controller = new LocalizationController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(isLocalUrl);
            controller.Url = urlHelper.Object;

            return controller;
        }
    }
}