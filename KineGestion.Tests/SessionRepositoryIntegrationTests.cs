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

        [Fact]
        public async Task GetBillingFollowUpCandidatesAsync_ShouldReturnCompletedPendingSessionsInAgeWindow()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var asOfUtc = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

            int inWindowId = 0;
            int outsideWindowId = 0;
            int paidId = 0;
            int notCompletedId = 0;

            await using (var setupContext = new AppDbContext(options))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                var patient = new Patient
                {
                    Nombre = "Ana",
                    Apellido = "Ruiz",
                    DNI = "11222333",
                    FechaNacimiento = new DateTime(1991, 6, 12)
                };

                var professional = new Professional
                {
                    Nombre = "Luis",
                    Apellido = "Perez",
                    Matricula = "MAT-301",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.Add(professional);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Rehabilitación",
                    CantidadSesionesTotales = 10,
                    FechaInicio = asOfUtc.AddDays(-30)
                };

                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                var inWindow = new Session
                {
                    FechaHora = asOfUtc.Date.AddDays(-2).AddHours(9),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 1,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Pending
                };

                var outsideWindow = new Session
                {
                    FechaHora = asOfUtc.Date.AddDays(-10).AddHours(9),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 2,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Pending
                };

                var paid = new Session
                {
                    FechaHora = asOfUtc.Date.AddDays(-3).AddHours(9),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 3,
                    Status = SessionStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid
                };

                var notCompleted = new Session
                {
                    FechaHora = asOfUtc.Date.AddDays(-1).AddHours(9),
                    PatientId = patient.Id,
                    ProfessionalId = professional.Id,
                    TreatmentId = treatment.Id,
                    NroSesionEnTratamiento = 4,
                    Status = SessionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending
                };

                setupContext.Sessions.AddRange(inWindow, outsideWindow, paid, notCompleted);
                await setupContext.SaveChangesAsync();

                inWindowId = inWindow.Id;
                outsideWindowId = outsideWindow.Id;
                paidId = paid.Id;
                notCompletedId = notCompleted.Id;
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);
                var result = (await repository.GetBillingFollowUpCandidatesAsync(asOfUtc, minAgeDays: 1, maxAgeDays: 7)).ToList();

                Assert.Single(result);
                var candidate = result.Single();
                Assert.Equal(inWindowId, candidate.SessionId);
                Assert.Equal(2, candidate.PaymentAgeDays);
                Assert.Contains("Ruiz", candidate.PacienteNombre, StringComparison.OrdinalIgnoreCase);

                Assert.DoesNotContain(result, c => c.SessionId == outsideWindowId);
                Assert.DoesNotContain(result, c => c.SessionId == paidId);
                Assert.DoesNotContain(result, c => c.SessionId == notCompletedId);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetKpiSegmentsByProfessionalAsync_ShouldAggregateKpisPerProfessional()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var from = new DateTime(2026, 6, 1);
            var to = new DateTime(2026, 7, 1);

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

                var prof1 = new Professional
                {
                    Nombre = "Jose",
                    Apellido = "Diaz",
                    Matricula = "MAT-200",
                    Especialidad = "Kinesiologia"
                };

                var prof2 = new Professional
                {
                    Nombre = "Ana",
                    Apellido = "Gomez",
                    Matricula = "MAT-201",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.AddRange(prof1, prof2);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Postoperatorio",
                    CantidadSesionesTotales = 12,
                    FechaInicio = from
                };
                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                int counter = 1;

                void AddSession(Professional prof, DateTime fechaHora, SessionStatus status, PaymentStatus pay)
                {
                    setupContext.Sessions.Add(new Session
                    {
                        FechaHora = fechaHora,
                        PatientId = patient.Id,
                        ProfessionalId = prof.Id,
                        TreatmentId = treatment.Id,
                        NroSesionEnTratamiento = counter++,
                        Status = status,
                        PaymentStatus = pay
                    });
                }

                // Prof1: 1 completada pagada + 1 completada pendiente + 1 pendiente
                AddSession(prof1, from.AddDays(1).AddHours(9), SessionStatus.Completed, PaymentStatus.Paid);
                AddSession(prof1, from.AddDays(2).AddHours(10), SessionStatus.Completed, PaymentStatus.Pending);
                AddSession(prof1, from.AddDays(3).AddHours(11), SessionStatus.Pending, PaymentStatus.Pending);
                // Prof2: 1 cancelada + 1 completada pendiente
                AddSession(prof2, from.AddDays(1).AddHours(15), SessionStatus.Canceled, PaymentStatus.Pending);
                AddSession(prof2, from.AddDays(2).AddHours(16), SessionStatus.Completed, PaymentStatus.Pending);
                // Fuera de rango
                AddSession(prof1, to.AddDays(5).AddHours(9), SessionStatus.Completed, PaymentStatus.Paid);

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);
                var result = (await repository.GetKpiSegmentsByProfessionalAsync(from, to)).ToList();

                Assert.Equal(2, result.Count);

                var prof1Row = result.Single(r => r.SegmentKey == "1");
                Assert.Equal(3, prof1Row.Total);
                Assert.Equal(2, prof1Row.Completed);
                Assert.Equal(0, prof1Row.Canceled);
                Assert.Equal(1, prof1Row.CompletedPending);
                Assert.Equal(1, prof1Row.CompletedPaid);
                Assert.Contains("Diaz", prof1Row.SegmentLabel, StringComparison.OrdinalIgnoreCase);

                var prof2Row = result.Single(r => r.SegmentKey == "2");
                Assert.Equal(2, prof2Row.Total);
                Assert.Equal(1, prof2Row.Completed);
                Assert.Equal(1, prof2Row.Canceled);
                Assert.Equal(1, prof2Row.CompletedPending);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetKpiSegmentsByTimeSlotAsync_ShouldAggregateKpisByHour()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var from = new DateTime(2026, 6, 1);
            var to = new DateTime(2026, 7, 1);

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

                var prof = new Professional
                {
                    Nombre = "Jose",
                    Apellido = "Diaz",
                    Matricula = "MAT-200",
                    Especialidad = "Kinesiologia"
                };

                setupContext.Patients.Add(patient);
                setupContext.Professionals.Add(prof);
                await setupContext.SaveChangesAsync();

                var treatment = new Treatment
                {
                    PatientId = patient.Id,
                    Descripcion = "Postoperatorio",
                    CantidadSesionesTotales = 12,
                    FechaInicio = from
                };
                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                int counter = 1;
                void AddSession(DateTime fechaHora, SessionStatus status, PaymentStatus pay)
                {
                    setupContext.Sessions.Add(new Session
                    {
                        FechaHora = fechaHora,
                        PatientId = patient.Id,
                        ProfessionalId = prof.Id,
                        TreatmentId = treatment.Id,
                        NroSesionEnTratamiento = counter++,
                        Status = status,
                        PaymentStatus = pay
                    });
                }

                // 2 en la hora 9 (1 pagada, 1 pendiente)
                AddSession(from.AddDays(1).AddHours(9), SessionStatus.Completed, PaymentStatus.Paid);
                AddSession(from.AddDays(2).AddHours(9), SessionStatus.Completed, PaymentStatus.Pending);
                // 1 en la hora 15 (cancelada)
                AddSession(from.AddDays(1).AddHours(15), SessionStatus.Canceled, PaymentStatus.Pending);

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);
                var result = (await repository.GetKpiSegmentsByTimeSlotAsync(from, to)).ToList();

                Assert.Equal(2, result.Count);

                var hour9 = result.Single(r => r.SegmentKey == "09");
                Assert.Equal(2, hour9.Total);
                Assert.Equal(2, hour9.Completed);
                Assert.Equal(1, hour9.CompletedPaid);
                Assert.Equal(1, hour9.CompletedPending);
                Assert.Equal("09:00", hour9.SegmentLabel);

                var hour15 = result.Single(r => r.SegmentKey == "15");
                Assert.Equal(1, hour15.Total);
                Assert.Equal(1, hour15.Canceled);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task CountLateCancellationsInRangeAsync_ShouldCountOnlyCancelationsWithLessThan24hLead()
        {
            var databaseName = $"KineGestion_Integration_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var from = new DateTime(2026, 5, 1);
            var to = new DateTime(2026, 6, 1);

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
                    FechaInicio = from
                };
                setupContext.Treatments.Add(treatment);
                await setupContext.SaveChangesAsync();

                int counter = 1;
                void AddCancelled(DateTime fechaHora, DateTime? cancelledAt)
                {
                    setupContext.Sessions.Add(new Session
                    {
                        FechaHora = fechaHora,
                        PatientId = patient.Id,
                        ProfessionalId = professional.Id,
                        TreatmentId = treatment.Id,
                        NroSesionEnTratamiento = counter++,
                        Status = SessionStatus.Canceled,
                        CancellationReason = CancellationReason.Olvido,
                        CancelledAt = cancelledAt
                    });
                }

                // Tardía: cancelada 2h antes del turno -> dentro de 24h
                AddCancelled(new DateTime(2026, 5, 10, 9, 0, 0), new DateTime(2026, 5, 10, 7, 0, 0));
                // Tardía: cancelada 23h antes del turno -> dentro de 24h
                AddCancelled(new DateTime(2026, 5, 12, 9, 0, 0), new DateTime(2026, 5, 11, 10, 0, 0));
                // Temprana: cancelada 48h antes del turno -> fuera de 24h
                AddCancelled(new DateTime(2026, 5, 15, 9, 0, 0), new DateTime(2026, 5, 13, 9, 0, 0));
                // Fuera de rango (junio)
                AddCancelled(new DateTime(2026, 6, 5, 9, 0, 0), new DateTime(2026, 6, 5, 7, 0, 0));

                await setupContext.SaveChangesAsync();
            }

            await using (var testContext = new AppDbContext(options))
            {
                var repository = new SessionRepository(testContext);
                var count = await repository.CountLateCancellationsInRangeAsync(from, to);

                Assert.Equal(2, count);
            }

            await using (var cleanupContext = new AppDbContext(options))
            {
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
    }
}
