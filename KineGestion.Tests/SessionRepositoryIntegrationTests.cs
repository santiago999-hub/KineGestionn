using System;
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
    }
}
