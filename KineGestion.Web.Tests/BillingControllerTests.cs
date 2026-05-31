using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Moq;

namespace KineGestion.Web.Tests
{
    public class BillingControllerTests
    {
        [Fact]
        public async Task Index_ShouldPopulateDashboard_UsingDirectCounters()
        {
            var sessionService = new Mock<ISessionService>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:DefaultSessionAmount"] = "2500"
                })
                .Build();

            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(6);
            sessionService.Setup(s => s.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(10);
            sessionService.Setup(s => s.CountByStatusAndPaymentStatusInRangeAsync(SessionStatus.Completed, PaymentStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(4);
            sessionService.Setup(s => s.GetPagedListForAdminAsync(
                    1,
                    10,
                    null,
                    SessionStatus.Completed,
                    PaymentStatus.Pending,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    "fecha",
                    "desc"))
                .ReturnsAsync((new[]
                {
                    new SessionListDto(1, DateTime.UtcNow, SessionStatus.Completed, PaymentStatus.Pending, 1, "Paciente", "Pro", "Tx", "Consultorio", false)
                }.AsEnumerable(), 1));

            var controller = BuildController(sessionService.Object, configuration);

            var result = await controller.Index(null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BillingDashboardViewModel>(view.Model);
            Assert.Equal(6, model.PendingCount);
            Assert.Equal(10, model.PaidCount);
            Assert.Equal(4, model.CompletedPendingCount);
            Assert.Equal(2500m, model.DefaultSessionAmount);
            Assert.Single(model.Items);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldReturnError_WhenNoIdsAreProvided()
        {
            var sessionService = new Mock<ISessionService>();
            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPaidBatch(new List<int>(), null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Seleccioná al menos una sesión pendiente para marcar como pagada.", controller.TempData["Error"]);
            sessionService.Verify(s => s.SetPaymentStatusAsync(It.IsAny<int>(), PaymentStatus.Paid), Times.Never);
        }

        [Fact]
        public async Task MarkPaidBatch_ShouldMarkDistinctPositiveIds_AsPaid()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService.Setup(s => s.SetPaymentStatusAsync(It.IsAny<int>(), PaymentStatus.Paid)).Returns(Task.CompletedTask);

            var controller = BuildController(sessionService.Object, new ConfigurationBuilder().Build());

            var result = await controller.MarkPaidBatch(new List<int> { 7, 7, 0, -2, 9 }, null, null, null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("2 sesiones marcadas como pagadas.", controller.TempData["Success"]);
            sessionService.Verify(s => s.SetPaymentStatusAsync(7, PaymentStatus.Paid), Times.Once);
            sessionService.Verify(s => s.SetPaymentStatusAsync(9, PaymentStatus.Paid), Times.Once);
            sessionService.Verify(s => s.SetPaymentStatusAsync(It.Is<int>(id => id <= 0), PaymentStatus.Paid), Times.Never);
        }

        private static BillingController BuildController(ISessionService sessionService, IConfiguration configuration)
        {
            var controller = new BillingController(sessionService, configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }
    }
}
