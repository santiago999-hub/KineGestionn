using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class AuditLogRepositoryIntegrationTests
    {
        [Fact]
        public async Task GetPagedAsync_ShouldFilterByActionAndDateRange()
        {
            var databaseName = $"KineGestion_AuditRepo_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.AuditLogs.AddRange(
                    new AuditLog
                    {
                        EntityName = "Patient",
                        EntityId = "1",
                        Action = "Create",
                        ChangedBy = "admin@local",
                        ChangedAt = new DateTime(2026, 5, 6, 8, 0, 0)
                    },
                    new AuditLog
                    {
                        EntityName = "Patient",
                        EntityId = "1",
                        Action = "Update",
                        ChangedBy = "admin@local",
                        ChangedAt = new DateTime(2026, 5, 7, 10, 30, 0)
                    },
                    new AuditLog
                    {
                        EntityName = "Patient",
                        EntityId = "2",
                        Action = "Delete",
                        ChangedBy = "admin@local",
                        ChangedAt = new DateTime(2026, 5, 8, 9, 0, 0)
                    },
                    new AuditLog
                    {
                        EntityName = "Session",
                        EntityId = "20",
                        Action = "Delete",
                        ChangedBy = "kine@local",
                        ChangedAt = new DateTime(2026, 5, 9, 9, 0, 0)
                    });

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new AuditLogRepository(testContext);

                var (items, totalCount) = await repository.GetPagedAsync(
                    entityName: "Patient",
                    entityId: null,
                    changedBy: "admin",
                    action: "Delete",
                    dateFrom: new DateTime(2026, 5, 8),
                    dateTo: new DateTime(2026, 5, 8),
                    page: 1,
                    pageSize: 10);

                var list = items.ToList();

                Assert.Equal(1, totalCount);
                Assert.Single(list);
                Assert.Equal("Patient", list[0].EntityName);
                Assert.Equal("2", list[0].EntityId);
                Assert.Equal("Delete", list[0].Action);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
    }
}
