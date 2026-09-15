using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;

namespace KineGestion.Web.Tests
{
    public class BillingOperationalAlertServiceTests
    {
        private static readonly DateTime Reference = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task GetSnapshotAsync_ShouldDetectConsecutiveLowWeeks_FromBillingBatchEvents()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    // Dos semanas consecutivas recientes con efectividad baja
                    BatchEvent(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchEvent(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });

            var service = BuildService(batchRepository, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()));

            var snapshot = await service.GetSnapshotAsync(Reference);

            Assert.Equal(4, snapshot.TrendPoints.Count);
            Assert.True(snapshot.HasConsecutiveLowWeeks);
            Assert.Equal(70m, snapshot.ThresholdPct);
        }

        [Fact]
        public async Task GetSnapshotAsync_Should_NotFlag_WhenNoBatches()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<BillingBatchEvent>());

            var service = BuildService(batchRepository, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()));

            var snapshot = await service.GetSnapshotAsync(Reference);

            Assert.False(snapshot.HasConsecutiveLowWeeks);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldNotQueue_WhenNoConsecutiveLowWeeks()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<BillingBatchEvent>());

            var queue = new Mock<IReminderDispatchQueue>();
            var service = BuildService(batchRepository, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()), queue);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.False(result.Queued);
            Assert.False(result.AlreadySentToday);
            queue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldQueue_WhenConsecutiveLowWeeksAndAdminConfigured()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    BatchEvent(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchEvent(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });

            var dispatchRepository = new Mock<IDispatchEventRepository>();
            dispatchRepository
                .Setup(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(0);

            ReminderDispatchWorkItem? queuedItem = null;
            var queue = new Mock<IReminderDispatchQueue>();
            queue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()))
                .Callback<ReminderDispatchWorkItem, CancellationToken>((item, _) => queuedItem = item)
                .Returns(new ValueTask());

            var config = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Reminders:OperationalAlerts:Enabled"] = "true",
                ["Reminders:OperationalAlerts:AdminEmail"] = "admin@clinic.com"
            });

            var service = BuildService(batchRepository, config, new MemoryCache(new MemoryCacheOptions()), queue, dispatchRepository);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.True(result.Queued);
            Assert.NotNull(queuedItem);
            Assert.Equal("admin@clinic.com", queuedItem!.PacienteEmail);
            Assert.Equal("BillingBatchLowEffectivenessAlert", queuedItem.DispatchType);
            Assert.Contains($"Umbral configurado: {70m:N2}%", queuedItem.EmailBodyOverride);
            dispatchRepository.Verify(
                r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
                Times.Once);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldNotDuplicate_WhenAlertAlreadySentToday()
        {
            var batchRepository = new Mock<IBillingBatchEventRepository>();
            batchRepository
                .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    BatchEvent(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchEvent(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });

            var dispatchRepository = new Mock<IDispatchEventRepository>();
            dispatchRepository
                .Setup(r => r.CountByTypeAsync("BillingBatchLowEffectivenessAlert", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            var queue = new Mock<IReminderDispatchQueue>();
            var service = BuildService(batchRepository, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()), queue, dispatchRepository);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.True(result.AlreadySentToday);
            Assert.False(result.Queued);
            queue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static BillingOperationalAlertService BuildService(
            Mock<IBillingBatchEventRepository> batchRepository,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            Mock<IReminderDispatchQueue>? queue = null,
            Mock<IDispatchEventRepository>? dispatchRepository = null)
            => new(
                batchRepository.Object,
                dispatchRepository?.Object ?? new Mock<IDispatchEventRepository>().Object,
                (queue ?? new Mock<IReminderDispatchQueue>()).Object,
                configuration,
                memoryCache);

        private static BillingBatchEvent BatchEvent(DateTime createdAtUtc, int requested, int updated)
            => new()
            {
                Operation = "MarkPaidBatch",
                RequestedCount = requested,
                UpdatedCount = updated,
                SkippedCount = 0,
                CreatedAtUtc = createdAtUtc,
                ChangedBy = "system"
            };

        private static IConfiguration BuildEmptyConfiguration()
            => BuildConfiguration(new Dictionary<string, string?>());

        private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
            => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}