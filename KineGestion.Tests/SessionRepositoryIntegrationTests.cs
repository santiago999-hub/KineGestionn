using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    public class SessionRepositoryIntegrationTests
    {
        [Fact]
        public async Task CountByStatusOnDateAsync_ShouldCountOnlyMatchingStatusAndDay()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var targetDay = new DateTime(2026, 5, 8);

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                var patient = new Patient
                {
                    Nombre = "Maria",
                    Apellido = "Lopez",
                    DNI = "87654321",
                    FechaNacimiento = new DateTime(1989, 4, 10)
                };

                var professional = new Professional
                {
                    Nombre = "Jose",
                    Apellido = "Diaz",
                    Matricula = "MAT-200",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.Add(professional);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Postoperatorio",
                    CantidadSesionesTotales = 12,
                    FechaInicio = targetDay
                };

                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                setupContext.Sessions.Add(new Session
                {
                    FechaHora = targetDay.AddHours(9),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 1,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid
                });

                setupContext.Sessions.Add(new Session
                {
                    FechaHora = targetDay.AddHours(11),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 2,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid
                });

                setupContext.Sessions.Add(new Session
                {
                    FechaHora = targetDay.AddHours(15),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 3,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                });

                setupContext.Sessions.Add(new Session
                {
                    FechaHora = targetDay.AddDays(1).AddHours(10),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 4,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid
                });

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);

                var completedCount = await repository.CountByStatusOnDateAsync(SessionStatus.Completed, targetDay);
                var pendingCount = await repository.CountByStatusOnDateAsync(SessionStatus.Pending, targetDay);

                Assert.Equal(2, completedCount);
                Assert.Equal(1, pendingCount);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task AddAsync_ShouldThrowBusinessValidationException_WhenUniqueIndexConflictsWithCountBasedNumbering()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                var patient = new Patient
                {
                    Nombre = "Juan",
                    Apellido = "Perez",
                    DNI = "12345678",
                    FechaNacimiento = new DateTime(1990, 1, 1)
                };

                var professional = new Professional
                {
                    Nombre = "Ana",
                    Apellido = "Gomez",
                    Matricula = "MAT-100",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.Add(professional);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Rehabilitacion",
                    CantidadSesionesTotales = 10,
                    FechaInicio = DateTime.UtcNow.Date
                };

                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                // Se deja un hueco intencional (1 y 3) para que count=2 intente insertar nro=3 y choque el índice único.
                setupContext.Sessions.Add(new Session
                {
                    FechaHora = DateTime.UtcNow.AddHours(-2),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 1,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                });

                setupContext.Sessions.Add(new Session
                {
                    FechaHora = DateTime.UtcNow.AddHours(-1),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 3,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                });

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);

                var newSession = new Session
                {
                    FechaHora = DateTime.UtcNow,
                    PatientId = 1,
                    ProfessionalId = 1,
                    TreatmentId = 1,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                };

                var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => repository.AddAsync(newSession));

                Assert.Equal(nameof(Session.NroSesionEnTratamiento), ex.PropertyName);
                Assert.Contains("numeración de sesión", ex.Message, StringComparison.OrdinalIgnoreCase);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task MarkCompletedPendingAsPaidBatchAsync_ShouldUpdateOnlyEligibleSessions_UsingSetBasedUpdate()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            int eligibleId;
            int alreadyPaidId;
            int notCompletedId;

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                var patient = new Patient
                {
                    Nombre = "Laura",
                    Apellido = "Ruiz",
                    DNI = "33445566",
                    FechaNacimiento = new DateTime(1992, 2, 2)
                };

                var professional = new Professional
                {
                    Nombre = "Pedro",
                    Apellido = "Suarez",
                    Matricula = "MAT-300",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.Add(professional);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Dolor lumbar",
                    CantidadSesionesTotales = 8,
                    FechaInicio = DateTime.UtcNow.Date
                };

                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                var eligible = new Session
                {
                    FechaHora = DateTime.UtcNow.AddHours(-3),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 1,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Pending
                };

                var alreadyPaid = new Session
                {
                    FechaHora = DateTime.UtcNow.AddHours(-2),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 2,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid
                };

                var notCompleted = new Session
                {
                    FechaHora = DateTime.UtcNow.AddHours(-1),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 3,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                };

                setupContext.Sessions.AddRange(eligible, alreadyPaid, notCompleted);
                await setupContext.SaveChangesAsync();

                eligibleId = eligible.Id;
                alreadyPaidId = alreadyPaid.Id;
                notCompletedId = notCompleted.Id;
            }

            var actionAtUtc = new DateTime(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc);

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);
                var result = await repository.MarkCompletedPendingAsPaidBatchAsync(new[] { eligibleId, alreadyPaidId, notCompletedId }, actionAtUtc);

                Assert.Equal(1, result.UpdatedCount);
                Assert.Equal(2, result.SkippedCount);
            }

            await using (var assertContext = new AppDbContext(options))
            {
                var sessions = await assertContext.Sessions
                    .AsNoTracking()
                    .Where(s => s.Id == eligibleId || s.Id == alreadyPaidId || s.Id == notCompletedId)
                    .ToListAsync();

                var eligible = sessions.Single(s => s.Id == eligibleId);
                var alreadyPaid = sessions.Single(s => s.Id == alreadyPaidId);
                var notCompleted = sessions.Single(s => s.Id == notCompletedId);

                Assert.Equal(PaymentStatus.Paid, eligible.PaymentStatus);
                Assert.Contains("COBRO_REGISTRADO", eligible.InternalNotes ?? string.Empty, StringComparison.OrdinalIgnoreCase);

                Assert.Equal(PaymentStatus.Paid, alreadyPaid.PaymentStatus);
                Assert.Equal(PaymentStatus.Pending, notCompleted.PaymentStatus);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
    }
}
