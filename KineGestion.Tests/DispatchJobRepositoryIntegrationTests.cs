using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class DispatchJobRepositoryIntegrationTests
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

            // Migrate, no EnsureCreated: se verifica el esquema real (incluye el índice único filtrado).
            await context.Database.MigrateAsync();
            return context;
        }

        private static DispatchJob NewJob(int sessionId, string dispatchType, string payloadHash = "ABC")
            => new DispatchJob
            {
                SessionId = sessionId,
                DispatchType = dispatchType,
                PayloadJson = "{\"SessionId\":" + sessionId + "}",
                PayloadHash = payloadHash,
                Status = DispatchJobStatus.Pending,
                Attempts = 0,
                CreatedAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
            };

        [Fact]
        public async Task AddAndFindOpen_ShouldReturnJobByDedupeKey()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);
                var job = NewJob(42, "PatientReminder", "HASH-1");

                await repository.AddAsync(job, default);

                var found = await repository.FindOpenAsync(42, "PatientReminder", "HASH-1", default);
                Assert.NotNull(found);
                Assert.Equal(DispatchJobStatus.Pending, found.Status);

                var otherType = await repository.FindOpenAsync(42, "PatientReminder", "HASH-2", default);
                Assert.Null(otherType);

                var otherSession = await repository.FindOpenAsync(43, "PatientReminder", "HASH-1", default);
                Assert.Null(otherSession);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task OpenJobs_ShouldBeUnique_AndTerminalJobs_ShouldAllowNewRow()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";

            // Fase 1: el índice único filtrado rechaza un segundo job abierto idéntico.
            await using (var context = await CreateDatabaseAsync(databaseName))
            {
                var repository = new DispatchJobRepository(context);
                await repository.AddAsync(NewJob(7, "BillingFollowUp:Firm", "DUP"), default);

                await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(NewJob(7, "BillingFollowUp:Firm", "DUP"), default));
            }

            // Fase 2 (contexto/scope nuevo, como en producción): al marcar el job como
            // terminal, un job idéntico vuelve a ser válido (solo bloquea jobs abiertos).
            await using (var context = new AppDbContext(await BuildOptionsAsync(databaseName)))
            {
                var repository = new DispatchJobRepository(context);
                var id = (await repository.FindOpenAsync(7, "BillingFollowUp:Firm", "DUP", default))!.Id;
                await repository.MarkSucceededAsync(id, null, DateTime.UtcNow, default);

                await repository.AddAsync(NewJob(7, "BillingFollowUp:Firm", "DUP"), default);
            }

            await using (var cleanup = new AppDbContext(await BuildOptionsAsync(databaseName)))
            {
                await cleanup.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task ClaimNextBatchAsync_ShouldClaimPendingInOrder_AndSkipClaimedJobs()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);
                var first = NewJob(1, "PatientReminder", "A");
                var second = NewJob(2, "PatientReminder", "B");
                first.CreatedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
                second.CreatedAtUtc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
                await repository.AddAsync(first, default);
                await repository.AddAsync(second, default);

                var now = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
                var claimed = await repository.ClaimNextBatchAsync(10, TimeSpan.FromHours(1), now, default);

                Assert.Equal(2, claimed.Count);
                Assert.Equal(new[] { 1, 2 }, claimed.Select(j => j.SessionId).ToArray());
                Assert.All(claimed, j => Assert.Equal(DispatchJobStatus.Processing, j.Status));
                Assert.NotNull(claimed[0].ClaimToken);

                // Segunda corrida: nada que reclamar dentro del lease.
                var secondClaim = await repository.ClaimNextBatchAsync(10, TimeSpan.FromHours(1), now.AddMinutes(30), default);
                Assert.Empty(secondClaim);

                // Fuera del lease, vuelven a ser reclamables.
                var afterLease = await repository.ClaimNextBatchAsync(10, TimeSpan.FromHours(1), now.AddHours(2), default);
                Assert.Equal(2, afterLease.Count);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task MarkFailedAsync_ShouldRetryUntilMaxAttempts_ThenBecomeTerminal()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);
                await repository.AddAsync(NewJob(1, "PatientReminder", "R"), default);

                var now = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
                var id = (await repository.FindOpenAsync(1, "PatientReminder", "R", default))!.Id;

                await repository.MarkFailedAsync(id, "boom", TimeSpan.FromMinutes(5), maxAttempts: 3, now, default);
                var afterFirst = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == id);
                Assert.Equal(DispatchJobStatus.Pending, afterFirst.Status);
                Assert.Equal(1, afterFirst.Attempts);
                Assert.Equal(now.AddMinutes(5), afterFirst.NextAttemptAtUtc);

                await repository.MarkFailedAsync(id, "boom", TimeSpan.FromMinutes(5), maxAttempts: 3, now.AddHours(1), default);
                var afterSecond = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == id);
                Assert.Equal(DispatchJobStatus.Pending, afterSecond.Status);
                Assert.Equal(2, afterSecond.Attempts);

                await repository.MarkFailedAsync(id, "boom", TimeSpan.FromMinutes(5), maxAttempts: 3, now.AddHours(2), default);
                var terminal = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == id);
                Assert.Equal(DispatchJobStatus.Failed, terminal.Status);
                Assert.Equal(3, terminal.Attempts);
                Assert.Null(terminal.NextAttemptAtUtc);
                Assert.Equal("boom", terminal.LastError);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task CleanupTerminalAsync_ShouldRemoveOnlyOldSucceededJobs()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);
                var oldSucceeded = NewJob(1, "PatientReminder", "S1");
                await repository.AddAsync(oldSucceeded, default);
                await repository.MarkSucceededAsync(oldSucceeded.Id, null, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), default);

                var newSucceeded = NewJob(2, "PatientReminder", "S2");
                await repository.AddAsync(newSucceeded, default);
                await repository.MarkSucceededAsync(newSucceeded.Id, null, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), default);

                var failed = NewJob(3, "PatientReminder", "F");
                await repository.AddAsync(failed, default);
                await repository.MarkFailedAsync(failed.Id, "err", TimeSpan.FromMinutes(1), maxAttempts: 1, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), default);

                await repository.CleanupTerminalAsync(new DateTime(2026, 9, 1, 1, 0, 0, DateTimeKind.Utc), default);

                var remaining = await context.DispatchJobs.AsNoTracking().Select(j => j.SessionId).ToListAsync();
                Assert.Equal(new[] { 2 }, remaining.OrderBy(x => x).ToArray());
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetDispatchedBillingTypesAsync_ShouldReturnOnlyOpenOrSucceededBillingTypes()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);

                await repository.AddAsync(NewJob(1, "BillingFollowUp:Soft", "1"), default);
                await repository.AddAsync(NewJob(2, "BillingFollowUp:Reminder", "2"), default);
                await repository.AddAsync(NewJob(3, "PatientReminder", "3"), default);

                var dispatched = await repository.GetDispatchedBillingTypesAsync(new[] { 1, 2, 3, 4 }, default);

                Assert.Single(dispatched[1]);
                Assert.Equal("BillingFollowUp:Soft", dispatched[1].Single());
                Assert.Equal("BillingFollowUp:Reminder", dispatched[2].Single());
                Assert.False(dispatched.ContainsKey(3));

                // Un job fallido terminal deja de contar como despachado.
                await repository.MarkFailedAsync((await repository.FindOpenAsync(1, "BillingFollowUp:Soft", "1", default))!.Id, "err", TimeSpan.FromMinutes(1), maxAttempts: 1, DateTime.UtcNow, default);
                var afterFail = await repository.GetDispatchedBillingTypesAsync(new[] { 1, 2, 3, 4 }, default);

                Assert.False(afterFail.ContainsKey(1));
                Assert.True(afterFail.ContainsKey(2));
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCountByStatusAndFlagStuckPending()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);
                var now = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

                // Reclamo el job en proceso ANTES de agregar el resto, para que el claim solo lo tome a él.
                var claimed = NewJob(3, "BillingFollowUp:Soft", "CLAIM");
                await repository.AddAsync(claimed, default);
                await repository.ClaimNextBatchAsync(10, TimeSpan.FromHours(1), now, default);

                var stuck = NewJob(1, "PatientReminder", "STUCK");
                stuck.CreatedAtUtc = now.AddMinutes(-60);
                await repository.AddAsync(stuck, default);

                var fresh = NewJob(2, "PatientReminder", "FRESH");
                fresh.CreatedAtUtc = now;
                await repository.AddAsync(fresh, default);

                var succeeded = NewJob(4, "PatientReminder", "OK");
                await repository.AddAsync(succeeded, default);
                await repository.MarkSucceededAsync(succeeded.Id, null, now, default);

                var failed = NewJob(5, "PatientReminder", "FAIL");
                await repository.AddAsync(failed, default);
                await repository.MarkFailedAsync(failed.Id, "boom", TimeSpan.FromMinutes(1), maxAttempts: 1, now, default);

                var stats = await repository.GetStatsAsync(now.AddMinutes(-15), default);

                Assert.Equal(2, stats.PendingCount);
                Assert.Equal(1, stats.ProcessingCount);
                Assert.Equal(1, stats.SucceededCount);
                Assert.Equal(1, stats.FailedCount);
                Assert.Equal(0, stats.CancelledCount);
                Assert.Equal(1, stats.StuckCount);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetJobsAsync_ShouldFilterStatusDispatchTypeSearch_AndPaginateDescending()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);

                var firmFail = NewJob(1, "BillingFollowUp:Firm", "F1");
                firmFail.PayloadJson = "{\"SessionId\":1,\"PacienteNombre\":\"Gomez, Ana\"}";
                await repository.AddAsync(firmFail, default);
                await repository.MarkFailedAsync(firmFail.Id, "SMTP offline", TimeSpan.FromMinutes(1), maxAttempts: 1, DateTime.UtcNow, default);

                var softOk = NewJob(2, "BillingFollowUp:Soft", "S1");
                softOk.PayloadJson = "{\"SessionId\":2,\"PacienteNombre\":\"Rodriguez, Luis\"}";
                await repository.AddAsync(softOk, default);
                await repository.MarkSucceededAsync(softOk.Id, null, DateTime.UtcNow, default);

                var reminder = NewJob(3, "PatientReminder", "R1");
                reminder.PayloadJson = "{\"SessionId\":3,\"PacienteNombre\":\"Gomez, Ana\"}";
                await repository.AddAsync(reminder, default);

                // Filtro por estado: solo fallidos.
                var failedOnly = await repository.GetJobsAsync(DispatchJobStatus.Failed, null, null, 1, 10, default);
                Assert.Single(failedOnly.Items);
                Assert.Equal(1, failedOnly.TotalCount);
                Assert.Equal("BillingFollowUp:Firm", failedOnly.Items[0].DispatchType);

                // Filtro por tipo de despacho.
                var billing = await repository.GetJobsAsync(null, "BillingFollowUp:Soft", null, 1, 10, default);
                Assert.Single(billing.Items);
                Assert.Equal(2, billing.Items[0].SessionId);

                // Búsqueda de texto: encuentra por nombre en el payload.
                var search = await repository.GetJobsAsync(null, null, "Gomez, Ana", 1, 10, default);
                Assert.Equal(2, search.TotalCount);

                // Paginación: página de 2, ordenada de más reciente a más antigua.
                softOk.CreatedAtUtc = new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc);
                reminder.CreatedAtUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
                firmFail.CreatedAtUtc = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(default);

                var page = await repository.GetJobsAsync(null, null, null, 1, 2, default);
                Assert.Equal(3, page.TotalCount);
                Assert.Equal(new[] { 2, 3 }, page.Items.Select(j => j.SessionId).ToArray());
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task ResetForRetryAsync_ShouldResetFailedJobs_AndIgnoreTerminalOthers()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);

                var failed1 = NewJob(1, "PatientReminder", "A");
                await repository.AddAsync(failed1, default);
                await repository.MarkFailedAsync(failed1.Id, "err1", TimeSpan.FromMinutes(1), maxAttempts: 1, DateTime.UtcNow, default);

                var failed2 = NewJob(2, "BillingFollowUp:Soft", "B");
                await repository.AddAsync(failed2, default);
                await repository.MarkFailedAsync(failed2.Id, "err2a", TimeSpan.FromMinutes(1), maxAttempts: 3, DateTime.UtcNow.AddMinutes(-10), default);
                await repository.MarkFailedAsync(failed2.Id, "err2b", TimeSpan.FromMinutes(1), maxAttempts: 3, DateTime.UtcNow.AddMinutes(-5), default);
                await repository.MarkFailedAsync(failed2.Id, "err2c", TimeSpan.FromMinutes(1), maxAttempts: 3, DateTime.UtcNow.AddHours(1), default);

                var succeeded = NewJob(3, "PatientReminder", "C");
                await repository.AddAsync(succeeded, default);
                await repository.MarkSucceededAsync(succeeded.Id, null, DateTime.UtcNow, default);

                var now = new DateTime(2026, 9, 2, 15, 0, 0, DateTimeKind.Utc);
                var resetCount = await repository.ResetForRetryAsync(new[] { failed1.Id, failed2.Id, succeeded.Id, -1 }, now, default);

                Assert.Equal(2, resetCount);

                var afterFailed1 = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == failed1.Id);
                Assert.Equal(DispatchJobStatus.Pending, afterFailed1.Status);
                Assert.Equal(0, afterFailed1.Attempts);
                Assert.Equal(now, afterFailed1.NextAttemptAtUtc);
                Assert.Null(afterFailed1.LastError);

                var afterSucceeded = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == succeeded.Id);
                Assert.Equal(DispatchJobStatus.Succeeded, afterSucceeded.Status);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task CancelAsync_ShouldCancelOpenJobs_AndLeaveTerminalUntouched()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            await using var context = await CreateDatabaseAsync(databaseName);
            try
            {
                var repository = new DispatchJobRepository(context);

                var pending = NewJob(1, "PatientReminder", "P");
                await repository.AddAsync(pending, default);

                var claimed = NewJob(2, "BillingFollowUp:Reminder", "PR");
                await repository.AddAsync(claimed, default);
                await repository.ClaimNextBatchAsync(10, TimeSpan.FromHours(1), DateTime.UtcNow, default);

                var succeeded = NewJob(3, "PatientReminder", "OK");
                await repository.AddAsync(succeeded, default);
                await repository.MarkSucceededAsync(succeeded.Id, null, DateTime.UtcNow, default);

                var now = new DateTime(2026, 9, 2, 16, 0, 0, DateTimeKind.Utc);
                var cancelledCount = await repository.CancelAsync(new[] { pending.Id, claimed.Id, succeeded.Id }, now, default);

                Assert.Equal(2, cancelledCount);

                var afterPending = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == pending.Id);
                Assert.Equal(DispatchJobStatus.Cancelled, afterPending.Status);
                Assert.Equal(now, afterPending.ProcessedAtUtc);
                Assert.Equal("Cancelado por el usuario", afterPending.LastError);

                var afterClaimed = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == claimed.Id);
                Assert.Equal(DispatchJobStatus.Cancelled, afterClaimed.Status);

                var afterSucceeded = await context.DispatchJobs.AsNoTracking().SingleAsync(j => j.Id == succeeded.Id);
                Assert.Equal(DispatchJobStatus.Succeeded, afterSucceeded.Status);
            }
            finally
            {
                await context.Database.EnsureDeletedAsync();
            }
        }
    }
}