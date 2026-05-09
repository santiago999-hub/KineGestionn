using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class AuditLogIntegrationTests
    {
        [Fact]
        public async Task SaveChangesAsync_ShouldPersistRealEntityId_ForCreateAuditLog()
        {
            var databaseName = $"KineGestion_Audit_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            int patientId;

            await using (var context = new AppDbContext(options))
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                var patient = new Patient
                {
                    Nombre = "Carlos",
                    Apellido = "Mendez",
                    DNI = "45671234",
                    FechaNacimiento = new DateTime(1991, 3, 15)
                };

                context.Patients.Add(patient);
                await context.SaveChangesAsync();
                patientId = patient.Id;
            }

            await using (var verifyContext = new AppDbContext(options))
            {
                var createLog = await verifyContext.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.EntityName == nameof(Patient) && a.Action == "Create")
                    .OrderByDescending(a => a.Id)
                    .FirstOrDefaultAsync();

                Assert.NotNull(createLog);
                Assert.Equal(patientId.ToString(), createLog!.EntityId);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task SaveChangesAsync_ShouldRegisterSoftDeleteAsDelete_ForProfessionalAndOffice()
        {
            var databaseName = $"KineGestion_Audit_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.Professionals.Add(new Professional
                {
                    Nombre = "Ana",
                    Apellido = "Lopez",
                    Matricula = "MAT-901",
                    Especialidad = "Kinesiologia",
                    IsActivo = true
                });

                setupContext.Offices.Add(new Office
                {
                    Name = "Consultorio A",
                    IsActive = true
                });

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var professional = await testContext.Professionals.SingleAsync();
                var office = await testContext.Offices.SingleAsync();

                professional.IsActivo = false;
                office.IsActive = false;

                await testContext.SaveChangesAsync();

                var logs = await testContext.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.EntityName == nameof(Professional) || a.EntityName == nameof(Office))
                    .OrderBy(a => a.Id)
                    .ToListAsync();

                var professionalDelete = logs.LastOrDefault(a => a.EntityName == nameof(Professional));
                var officeDelete = logs.LastOrDefault(a => a.EntityName == nameof(Office));

                Assert.NotNull(professionalDelete);
                Assert.NotNull(officeDelete);
                Assert.Equal("Delete", professionalDelete!.Action);
                Assert.Equal("Delete", officeDelete!.Action);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
    }
}
