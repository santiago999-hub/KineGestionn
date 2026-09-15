using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KineGestion.Tests
{
    public class MigrationBusinessEventDataCopyTests
    {
        private const string TargetMigration = "20260911000515_AddSessionOfficeConflictIndex";

        [Fact]
        public async Task AddBusinessEventTables_ShouldCopyHistoricalAuditData()
        {
            var databaseName = $"KineGestion_Migration_{Guid.NewGuid():N}";
            var options = IntegrationTestDatabase.BuildOptions(databaseName);

            var billingChangedAt = new DateTime(2026, 5, 4, 9, 30, 0, DateTimeKind.Utc);
            var dispatchChangedAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc);
            var longSearch = new string('z', 300);

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.GetService<IMigrator>().MigrateAsync(TargetMigration);

                setupContext.AuditLogs.AddRange(
                    new AuditLog
                    {
                        EntityName = "BillingBatch",
                        EntityId = "1",
                        Action = "Create",
                        ChangedBy = "admin@local",
                        ChangedAt = billingChangedAt,
                        NewValuesJson = """
                            {"Operation":"MarkPaidBatch","RequestedCount":10,"UpdatedCount":7,"SkippedCount":3,
                             "Filters":{"DateFrom":"2026-05-04","DateTo":"2026-05-06","Search":"paciente","OnlyCompletedPending":true},
                             "ChangedBy":"admin@local"}
                            """
                    },
                    new AuditLog
                    {
                        EntityName = "BillingBatch",
                        EntityId = "2",
                        Action = "Create",
                        ChangedBy = "admin@local",
                        ChangedAt = billingChangedAt.AddMinutes(1),
                        NewValuesJson = "{\"Operation\":\"MarkPendingBatch\",\"RequestedCount\":2,\"UpdatedCount\":2,\"SkippedCount\":0," +
                            "\"Filters\":{\"DateFrom\":null,\"DateTo\":null,\"Search\":\"" + longSearch + "\",\"OnlyCompletedPending\":false}," +
                            "\"ChangedBy\":\"admin@local\"}"
                    },
                    new AuditLog
                    {
                        EntityName = "BillingBatch",
                        EntityId = "3",
                        Action = "Update",
                        ChangedBy = "admin@local",
                        ChangedAt = billingChangedAt.AddMinutes(2),
                        NewValuesJson = "{\"Operation\":\"Ignored\",\"RequestedCount\":1}"
                    },
                    new AuditLog
                    {
                        EntityName = "ReminderDispatch",
                        EntityId = "42",
                        Action = "Create",
                        ChangedBy = "scheduler@local",
                        ChangedAt = dispatchChangedAt,
                        NewValuesJson = """
                            {"DispatchType":"PatientReminder","SessionId":42,"EmailSent":true,"WhatsAppSent":false,
                             "Errors":["primer error","segundo error"]}
                            """
                    },
                    new AuditLog
                    {
                        EntityName = "BillingFollowUp",
                        EntityId = "17",
                        Action = "Create",
                        ChangedBy = "buy@local",
                        ChangedAt = dispatchChangedAt.AddMinutes(1),
                        NewValuesJson = "{\"DispatchType\":\"BillingFollowUp:PIN\",\"SessionId\":17,\"EmailSent\":false,\"WhatsAppSent\":true,\"Errors\":[\"solo uno\"]}"
                    },
                    new AuditLog
                    {
                        EntityName = "OperationalAlert",
                        EntityId = "0",
                        Action = "Create",
                        ChangedBy = "system",
                        ChangedAt = dispatchChangedAt.AddMinutes(2),
                        NewValuesJson = "{\"EmailSent\":false,\"WhatsAppSent\":false}"
                    },
                    new AuditLog
                    {
                        EntityName = "Patient",
                        EntityId = "9",
                        Action = "Update",
                        ChangedBy = "admin@local",
                        ChangedAt = dispatchChangedAt.AddMinutes(3),
                        NewValuesJson = "{}"
                    });

                await setupContext.SaveChangesAsync();
            }

            await using (var migrateContext = new AppDbContext(options))
            {
                await migrateContext.Database.MigrateAsync();
            }

            await using (var verifyContext = new AppDbContext(options))
            {
                var billingRows = await verifyContext.BillingBatchEvents.OrderBy(e => e.CreatedAtUtc).ToListAsync();
                var dispatchRows = await verifyContext.DispatchEvents.OrderBy(e => e.SentAtUtc).ToListAsync();

                Assert.Equal(2, billingRows.Count);
                Assert.Equal(3, dispatchRows.Count);

                var markPaid = billingRows[0];
                Assert.Equal("MarkPaidBatch", markPaid.Operation);
                Assert.Equal(10, markPaid.RequestedCount);
                Assert.Equal(7, markPaid.UpdatedCount);
                Assert.Equal(3, markPaid.SkippedCount);
                Assert.Equal(new DateTime(2026, 5, 4), markPaid.FilterDateFrom);
                Assert.Equal(new DateTime(2026, 5, 6), markPaid.FilterDateTo);
                Assert.Equal("paciente", markPaid.FilterSearch);
                Assert.True(markPaid.OnlyCompletedPending);
                Assert.Equal("admin@local", markPaid.ChangedBy);
                Assert.Equal(billingChangedAt, markPaid.CreatedAtUtc);

                var markPending = billingRows[1];
                Assert.Equal("MarkPendingBatch", markPending.Operation);
                Assert.Null(markPending.FilterDateFrom);
                Assert.Null(markPending.FilterDateTo);
                Assert.Equal(200, markPending.FilterSearch!.Length);
                Assert.StartsWith(new string('z', 200), markPending.FilterSearch);
                Assert.False(markPending.OnlyCompletedPending);

                var patientReminder = dispatchRows[0];
                Assert.Equal("PatientReminder", patientReminder.DispatchType);
                Assert.Equal(42, patientReminder.SessionId);
                Assert.True(patientReminder.EmailSent);
                Assert.False(patientReminder.WhatsAppSent);
                Assert.Equal("primer error | segundo error", patientReminder.Errors);
                Assert.Equal(dispatchChangedAt, patientReminder.SentAtUtc);

                var followUp = dispatchRows[1];
                Assert.Equal("BillingFollowUp:PIN", followUp.DispatchType);
                Assert.Equal(17, followUp.SessionId);
                Assert.False(followUp.EmailSent);
                Assert.True(followUp.WhatsAppSent);
                Assert.Equal("solo uno", followUp.Errors);

                var alert = dispatchRows[2];
                Assert.Equal("BillingBatchLowEffectivenessAlert", alert.DispatchType);
                Assert.Null(alert.SessionId);
                Assert.False(alert.EmailSent);
                Assert.False(alert.WhatsAppSent);
                Assert.Null(alert.Errors);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
    }
}