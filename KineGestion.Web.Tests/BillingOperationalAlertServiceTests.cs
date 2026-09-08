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
        public async Task GetSnapshotAsync_ShouldDetectConsecutiveLowWeeks_FromBillingBatchLogs()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    // Dos semanas consecutivas recientes con efectividad baja
                    BatchLog(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchLog(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });

            var service = BuildService(auditLogService, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()));

            var snapshot = await service.GetSnapshotAsync(Reference);

            Assert.Equal(4, snapshot.TrendPoints.Count);
            Assert.True(snapshot.HasConsecutiveLowWeeks);
            Assert.Equal(70m, snapshot.ThresholdPct);
        }

        [Fact]
        public async Task GetSnapshotAsync_Should_NotFlag_WhenNoBatches()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Enumerable.Empty<AuditLog>());

            var service = BuildService(auditLogService, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()));

            var snapshot = await service.GetSnapshotAsync(Reference);

            Assert.False(snapshot.HasConsecutiveLowWeeks);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldNotQueue_WhenNoConsecutiveLowWeeks()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Enumerable.Empty<AuditLog>());

            var queue = new Mock<IReminderDispatchQueue>();
            var service = BuildService(auditLogService, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()), queue);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.False(result.Queued);
            Assert.False(result.AlreadySentToday);
            queue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldQueue_WhenConsecutiveLowWeeksAndAdminConfigured()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    BatchLog(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchLog(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });
            auditLogService
                .Setup(a => a.GetAllAsync("OperationalAlert", It.IsAny<string?>(), null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Enumerable.Empty<AuditLog>());

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

            var service = BuildService(auditLogService, config, new MemoryCache(new MemoryCacheOptions()), queue);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.True(result.Queued);
            Assert.NotNull(queuedItem);
            Assert.Equal("admin@clinic.com", queuedItem!.PacienteEmail);
            Assert.Equal("BillingBatchLowEffectivenessAlert", queuedItem.DispatchType);
            Assert.Equal("OperationalAlert", queuedItem.AuditEntityName);
            Assert.Contains("Umbral configurado: 70,00%", queuedItem.EmailBodyOverride);
        }

        [Fact]
        public async Task QueueAlertIfNeededAsync_ShouldNotDuplicate_WhenAlertAlreadySentToday()
        {
            var auditLogService = new Mock<IAuditLogService>();
            auditLogService
                .Setup(a => a.GetAllAsync("BillingBatch", null, null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[]
                {
                    BatchLog(new DateTime(2026, 9, 2, 10, 0, 0), requested: 10, updated: 2),
                    BatchLog(new DateTime(2026, 8, 28, 10, 0, 0), requested: 8, updated: 1)
                });
            auditLogService
                .Setup(a => a.GetAllAsync("OperationalAlert", It.IsAny<string?>(), null, "Create", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new[] { new AuditLog { EntityName = "OperationalAlert", Action = "Create" } });

            var queue = new Mock<IReminderDispatchQueue>();
            var service = BuildService(auditLogService, BuildEmptyConfiguration(), new MemoryCache(new MemoryCacheOptions()), queue);

            var result = await service.QueueAlertIfNeededAsync("admin@local", Reference);

            Assert.True(result.AlreadySentToday);
            Assert.False(result.Queued);
            queue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static BillingOperationalAlertService BuildService(
            Mock<IAuditLogService> auditLogService,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            Mock<IReminderDispatchQueue>? queue = null)
            => new(
                auditLogService.Object,
                (queue ?? new Mock<IReminderDispatchQueue>()).Object,
                configuration,
                memoryCache);

        private static AuditLog BatchLog(DateTime changedAt, int requested, int updated)
            => new()
            {
                EntityName = "BillingBatch",
                Action = "Create",
                ChangedAt = changedAt,
                ChangedBy = "system",
                NewValuesJson = $"{{\"RequestedCount\":{requested},\"UpdatedCount\":{updated},\"SkippedCount\":0}}"
            };

        private static IConfiguration BuildEmptyConfiguration()
            => BuildConfiguration(new Dictionary<string, string?>());

        private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
            => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
