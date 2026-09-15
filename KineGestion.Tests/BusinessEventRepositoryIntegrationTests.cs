using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Repositories;

namespace KineGestion.Tests
{
    public class BusinessEventRepositoryIntegrationTests
    {
        [Fact]
        public async Task BillingBatchEvents_AddAndGetByDateRange_ShouldFilterInclusiveByDay()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await IntegrationTestDatabase.CreateMigratedAsync(databaseName);
            try
            {
                var repository = new BillingBatchEventRepository(context);

                await repository.AddAsync(BatchEvent(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), "MarkPaidBatch", 10, 7));
                await repository.AddAsync(BatchEvent(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), "MarkPaidBatch", 8, 6));
                await repository.AddAsync(BatchEvent(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), "MarkPendingBatch", 5, 4));

                // dateFrom y dateTo se interpretan por día completo (>= dateFrom.Date, < dateTo.Date+1).
                var between = await repository.GetByDateRangeAsync(
                    new DateTime(2026, 9, 2, 14, 30, 0, DateTimeKind.Utc),
                    new DateTime(2026, 9, 3, 8, 30, 0, DateTimeKind.Utc));

                Assert.Equal(new[] { 3, 2 }, between.Select(e => e.Id).ToArray());
                Assert.Equal(new[] { "MarkPendingBatch", "MarkPaidBatch" }, between.Select(e => e.Operation).ToArray());

                var afterFirst = await repository.GetByDateRangeAsync(new DateTime(2026, 9, 3), null);
                Assert.Single(afterFirst);

                var beforeThird = await repository.GetByDateRangeAsync(null, new DateTime(2026, 9, 1));
                Assert.Single(beforeThird);
                Assert.Equal(1, beforeThird[0].Id);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task BillingBatchEvents_GetByDateRange_ShouldReturnEmpty_WhenNoEvents()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await IntegrationTestDatabase.CreateMigratedAsync(databaseName);
            try
            {
                var repository = new BillingBatchEventRepository(context);

                var result = await repository.GetByDateRangeAsync(null, null);

                Assert.Empty(result);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task DispatchEvents_GetByType_ShouldFilterExactType_AndFullDay()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await IntegrationTestDatabase.CreateMigratedAsync(databaseName);
            try
            {
                var repository = new DispatchEventRepository(context);

                await repository.AddAsync(DispatchEventType("PatientReminder", 1, new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("PatientReminder", 2, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("BillingFollowUp:Soft", 3, new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("BillingBatchLowEffectivenessAlert", null, new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc), email: true));

                var patientReminders = await repository.GetByTypeAsync("PatientReminder", null, null);
                Assert.Equal(new[] { 2, 1 }, patientReminders.Select(e => e.SessionId!.Value).ToArray());

                // Día completo: >= 9/2 00:00 y < 9/3 00:00.
                var onSecond = await repository.GetByTypeAsync("PatientReminder", new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc));
                Assert.Single(onSecond);
                Assert.Equal(2, onSecond[0].SessionId);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task DispatchEvents_GetByTypePrefix_ShouldReturnMatchingTypes_OrderedDescending()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await IntegrationTestDatabase.CreateMigratedAsync(databaseName);
            try
            {
                var repository = new DispatchEventRepository(context);

                await repository.AddAsync(DispatchEventType("BillingFollowUp:Soft", 1, new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("BillingFollowUp:Firm", 2, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("PatientReminder", 3, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), email: true));

                var followUps = await repository.GetByTypePrefixAsync("BillingFollowUp:", null, null);
                Assert.Equal(new[] { 2, 1 }, followUps.Select(e => e.SessionId!.Value).ToArray());
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task DispatchEvents_CountByType_ShouldCountOnlyMatchingType_OnGivenDay()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await IntegrationTestDatabase.CreateMigratedAsync(databaseName);
            try
            {
                var repository = new DispatchEventRepository(context);

                await repository.AddAsync(DispatchEventType("BillingBatchLowEffectivenessAlert", null, new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("BillingBatchLowEffectivenessAlert", null, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc), email: true));
                await repository.AddAsync(DispatchEventType("PatientReminder", 1, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), email: true));

                var dayStart = new DateTime(2026, 9, 2);
                var todayCount = await repository.CountByTypeAsync("BillingBatchLowEffectivenessAlert", dayStart, dayStart);
                Assert.Equal(1, todayCount);

                var allCount = await repository.CountByTypeAsync("BillingBatchLowEffectivenessAlert", null, null);
                Assert.Equal(2, allCount);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        private static BillingBatchEvent BatchEvent(DateTime createdAtUtc, string operation, int requested, int updated)
            => new()
            {
                Operation = operation,
                RequestedCount = requested,
                UpdatedCount = updated,
                SkippedCount = 0,
                FilterDateFrom = null,
                FilterDateTo = null,
                FilterSearch = null,
                OnlyCompletedPending = true,
                ChangedBy = "admin@local",
                CreatedAtUtc = createdAtUtc
            };

        private static DispatchEvent DispatchEventType(string dispatchType, int? sessionId, DateTime sentAtUtc, bool email)
            => new()
            {
                DispatchType = dispatchType,
                SessionId = sessionId,
                ChangedBy = "system",
                SentAtUtc = sentAtUtc,
                EmailSent = email,
                WhatsAppSent = false,
                Errors = null
            };
    }
}