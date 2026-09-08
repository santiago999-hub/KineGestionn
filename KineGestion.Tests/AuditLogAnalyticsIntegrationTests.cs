using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class AuditLogAnalyticsIntegrationTests
    {
        private static Task<DbContextOptions<AppDbContext>> BuildOptionsAsync(string databaseName)
        {
            var connectionString = TestConnection.For(databaseName);
            return Task.FromResult(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options);
        }

        private static async Task<AppDbContext> CreateDatabaseAsync(string databaseName)
        {
            var options = await BuildOptionsAsync(databaseName);

            var context = new AppDbContext(options);
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
            return context;
        }

        [Fact]
        public async Task GetAnalyticsAsync_ShouldAggregateByActionEntityUserAndDay()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new AuditLogRepository(context);

                await AddLogAsync(repository, "Patient", "1", "Create", "admin@local", new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
                await AddLogAsync(repository, "Patient", "2", "Update", "admin@local", new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc));
                await AddLogAsync(repository, "Session", "3", "Create", "lic@local", new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc));
                await AddLogAsync(repository, "Session", "4", "Delete", "lic@local", new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
                await AddLogAsync(repository, "Patient", "5", "Update", "admin@local", new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));

                var data = await repository.GetAnalyticsAsync(
                    new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));

                Assert.Equal(4, data.TotalCount);
                Assert.Equal(new DateTime(2026, 9, 1), data.FromUtc);
                Assert.Equal(new DateTime(2026, 9, 2), data.ToUtc);

                Assert.Equal(2, data.ByAction.Single(x => x.Name == "Create").Count);
                Assert.Equal(1, data.ByAction.Single(x => x.Name == "Update").Count);
                Assert.Equal(1, data.ByAction.Single(x => x.Name == "Delete").Count);

                Assert.Equal(2, data.ByEntity.Single(x => x.Name == "Patient").Count);
                Assert.Equal(2, data.ByEntity.Single(x => x.Name == "Session").Count);

                Assert.Equal(2, data.ByUser.Single(x => x.Name == "admin@local").Count);
                Assert.Equal(2, data.ByUser.Single(x => x.Name == "lic@local").Count);

                Assert.Equal(2, data.DailyTrend.Count);
                Assert.Equal(2, data.DailyTrend.Single(x => x.DateUtc.Date == new DateTime(2026, 9, 1)).Count);
                Assert.Equal(2, data.DailyTrend.Single(x => x.DateUtc.Date == new DateTime(2026, 9, 2)).Count);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetAnalyticsAsync_ShouldUseRecentWindow_WhenNoDatesProvided()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new AuditLogRepository(context);

                await AddLogAsync(repository, "Patient", "1", "Create", "admin@local", DateTime.UtcNow.Date.AddDays(-2).AddHours(10));
                await AddLogAsync(repository, "Patient", "2", "Create", "admin@local", DateTime.UtcNow.Date.AddDays(-120));

                var data = await repository.GetAnalyticsAsync(null, null);

                Assert.Equal(1, data.TotalCount);
                Assert.True(data.FromUtc >= DateTime.UtcNow.Date.AddDays(-90));
                Assert.Single(data.DailyTrend);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        private static async Task AddLogAsync(AuditLogRepository repository, string entity, string entityId, string action, string changedBy, DateTime changedAt)
        {
            await repository.AddAsync(new AuditLog
            {
                EntityName = entity,
                EntityId = entityId,
                Action = action,
                ChangedBy = changedBy,
                ChangedAt = changedAt,
                OldValuesJson = "{}",
                NewValuesJson = "{}"
            });
        }
    }
}