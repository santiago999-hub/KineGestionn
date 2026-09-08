using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KineGestion.Web.Tests
{
    public class DispatchQueueControllerTests
    {
        [Fact]
        public async Task Index_ShouldBuildViewModel_WithStatsFiltersAndPaging()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.GetStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DispatchQueueStats { PendingCount = 3, StuckCount = 2, FailedCount = 1 });
            repository
                .Setup(r => r.GetJobsAsync(DispatchJobStatus.Failed, "BillingFollowUp:Soft", "gomez", 2, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new DispatchJob[] { NewJob(1), NewJob(2) }, 41));
            repository
                .Setup(r => r.GetDistinctDispatchTypesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new string[] { "BillingFollowUp:Soft", "PatientReminder" });

            var controller = BuildController(repository.Object);
            controller.TempData = MakeTempData(controller);

            var result = await controller.Index(DispatchJobStatus.Failed, "BillingFollowUp:Soft", "gomez", 2);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DispatchQueueIndexViewModel>(view.Model);

            Assert.Equal(2, model.Jobs.Count);
            Assert.Equal(41, model.TotalCount);
            Assert.Equal(3, model.Stats.PendingCount);
            Assert.Equal(2, model.Stats.StuckCount);
            Assert.Equal(2, model.Page);
            Assert.Equal(3, model.TotalPages);
            Assert.Equal(20, model.PageSize);
            Assert.Equal(DispatchJobStatus.Failed, model.FilterStatus);
            Assert.Equal("gomez", model.Search);
            Assert.Equal(2, model.DispatchTypes.Count);

            Assert.NotNull(controller.TempData["Error"]);
            Assert.Contains("2 jobs pendientes llevan más de 30 minutos", (string)controller.TempData["Error"]!);
        }

        [Fact]
        public async Task Index_ShouldHaveStuckBanner_WhenStuckJobsExist()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.GetStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DispatchQueueStats { StuckCount = 5 });
            repository
                .Setup(r => r.GetJobsAsync(It.IsAny<DispatchJobStatus?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Array.Empty<DispatchJob>(), 0));
            repository
                .Setup(r => r.GetDistinctDispatchTypesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<string>());

            var controller = BuildController(repository.Object);
            controller.TempData = MakeTempData(controller);

            var result = await controller.Index();

            Assert.IsType<ViewResult>(result);
            Assert.NotNull(controller.TempData["Error"]);
            Assert.Contains("5 jobs pendientes", (string)controller.TempData["Error"]!);
        }

        [Fact]
        public async Task RetrySelected_ShouldResetFailedJobs_AndRedirectWithSuccess()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.ResetForRetryAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            var controller = BuildController(repository.Object);
            controller.TempData = MakeTempData(controller);

            var result = await controller.RetrySelected(new[] { 5, 6 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(DispatchQueueController.Index), redirect.ActionName);
            Assert.Contains("Reintentos programados: 2", (string)controller.TempData["Success"]!);

            repository.Verify(
                r => r.ResetForRetryAsync(
                    It.Is<IReadOnlyCollection<int>>(ids => ids.Count() == 2 && ids.Contains(5) && ids.Contains(6)),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RetryAllFailed_ShouldFetchFailedJobs_AndResetAll()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.GetJobsAsync(DispatchJobStatus.Failed, null, null, 1, 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new DispatchJob[] { NewJob(1), NewJob(2) }, 2));
            repository
                .Setup(r => r.ResetForRetryAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            var controller = BuildController(repository.Object);
            controller.TempData = MakeTempData(controller);

            var result = await controller.RetryAllFailed();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(DispatchQueueController.Index), redirect.ActionName);
            Assert.Contains("Reintentos programados: 2", (string)controller.TempData["Success"]!);

            repository.Verify(
                r => r.GetJobsAsync(DispatchJobStatus.Failed, null, null, 1, 1000, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelSelected_ShouldCancelAndRedirectWithSuccess()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.CancelAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var controller = BuildController(repository.Object);
            controller.TempData = MakeTempData(controller);

            var result = await controller.CancelSelected(new[] { 7 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(DispatchQueueController.Index), redirect.ActionName);
            Assert.Contains("Envíos cancelados: 1", (string)controller.TempData["Success"]!);
        }

        private static DispatchQueueController BuildController(IDispatchJobRepository repository)
            => new(
                repository,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                new Mock<ILogger<DispatchQueueController>>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

        private static TempDataDictionary MakeTempData(Controller controller)
            => new(controller.HttpContext, Mock.Of<ITempDataProvider>());

        private static DispatchJob NewJob(int id) => new()
        {
            Id = id,
            SessionId = id,
            DispatchType = "BillingFollowUp:Soft",
            PayloadJson = "{}",
            PayloadHash = "H",
            Status = DispatchJobStatus.Failed,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}