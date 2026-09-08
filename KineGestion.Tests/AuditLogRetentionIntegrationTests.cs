using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class AuditLogRetentionIntegrationTests
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
        public async Task DeleteOlderThanAsync_ShouldDeleteOnlyRowsStrictlyOlderThanCutoff()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new AuditLogRepository(context);
                var cutoff = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

                await AddLogAsync(repository, "Patient", "1", "Create", "admin@local", new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));
                await AddLogAsync(repository, "Patient", "2", "Update", "admin@local", new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc));
                await AddLogAsync(repository, "Patient", "3", "Create", "admin@local", cutoff); // exactamente en el límite: no se borra
                await AddLogAsync(repository, "Session", "4", "Update", "lic@local", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));

                var deleted = await repository.DeleteOlderThanAsync(cutoff, 1000, default);

                Assert.Equal(2, deleted);

                var remaining = await context.AuditLogs.AsNoTracking().OrderBy(a => a.ChangedAt).Select(a => a.EntityId).ToListAsync();
                Assert.Equal(new[] { "3", "4" }, remaining);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldIterateInBatches_AndReturnTotalDeleted()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new AuditLogRepository(context);
                var cutoff = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

                for (var i = 0; i < 5; i++)
                {
                    await AddLogAsync(repository, "Patient", $"old{i}", "Create", "admin@local", new DateTime(2026, 6, 1, 10, i, 0, DateTimeKind.Utc));
                }
                await AddLogAsync(repository, "Session", "recent", "Create", "lic@local", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));

                var deleted = await repository.DeleteOlderThanAsync(cutoff, 2, default);

                Assert.Equal(5, deleted);

                var remaining = await context.AuditLogs.AsNoTracking().Select(a => a.EntityId).ToListAsync();
                Assert.Single(remaining);
                Assert.Equal("recent", remaining[0]);
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