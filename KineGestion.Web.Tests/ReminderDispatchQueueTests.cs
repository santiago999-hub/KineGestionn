using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KineGestion.Web.Tests
{
    public class ReminderDispatchQueueTests
    {
        [Fact]
        public async Task QueueAsync_ShouldPersistJob_WithStablePayloadHash()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.FindOpenAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DispatchJob?)null);

            DispatchJob? persisted = null;
            repository
                .Setup(r => r.AddAsync(It.IsAny<DispatchJob>(), It.IsAny<CancellationToken>()))
                .Callback<DispatchJob, CancellationToken>((job, _) => persisted = job)
                .Returns(Task.CompletedTask);

            var queue = new ReminderDispatchQueue(repository.Object);
            var workItem = BuildWorkItem(5, "2026-09-01T10:00:00Z", "PatientReminder");

            await queue.QueueAsync(workItem);

            repository.Verify(r => r.AddAsync(It.IsAny<DispatchJob>(), default), Times.Once);
            Assert.NotNull(persisted);
            Assert.Equal(5, persisted.SessionId);
            Assert.Equal("PatientReminder", persisted.DispatchType);
            Assert.Equal(DispatchJobStatus.Pending, persisted.Status);
            Assert.NotNull(persisted.PayloadHash);

            var deserialized = JsonSerializer.Deserialize<ReminderDispatchWorkItem>(persisted.PayloadJson);
            Assert.NotNull(deserialized);
            Assert.Equal("Personal de Prueba", deserialized.PacienteNombre);
            Assert.Equal("https://localhost/confirm/5", deserialized.ConfirmUrl);

            // Hash estable: cambia actor/stamp pero no contenido → mismo hash.
            var another = BuildWorkItem(5, "2026-09-01T10:00:00Z", "PatientReminder");
            another.ChangedBy = "otro@usuario.com";
            another.EnqueuedAtUtc = DateTime.UtcNow.AddHours(3);

            DispatchJob? persistedTwo = null;
            repository
                .Setup(r => r.AddAsync(It.IsAny<DispatchJob>(), It.IsAny<CancellationToken>()))
                .Callback<DispatchJob, CancellationToken>((job, _) => persistedTwo = job)
                .Returns(Task.CompletedTask);

            await queue.QueueAsync(another);

            Assert.NotNull(persisted);
            Assert.NotNull(persistedTwo);
            Assert.Equal(persisted.PayloadHash, persistedTwo.PayloadHash);
        }

        [Fact]
        public async Task QueueAsync_ShouldSkipWhenAnOpenJobAlreadyExists()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .Setup(r => r.FindOpenAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DispatchJob { Id = 99, SessionId = 5, DispatchType = "PatientReminder" });

            var queue = new ReminderDispatchQueue(repository.Object);

            await queue.QueueAsync(BuildWorkItem(5, "2026-09-01T10:00:00Z", "PatientReminder"));

            repository.Verify(r => r.AddAsync(It.IsAny<DispatchJob>(), default), Times.Never);
        }

        [Fact]
        public async Task QueueAsync_ShouldTreatDuplicateKeyRaceAsIdempotent()
        {
            var repository = new Mock<IDispatchJobRepository>();
            repository
                .SetupSequence(r => r.FindOpenAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DispatchJob?)null) // primer chequeo: no existe
                .ReturnsAsync(new DispatchJob { Id = 50, SessionId = 5, DispatchType = "PatientReminder" }); // tras la excepción: sí existe

            repository
                .Setup(r => r.AddAsync(It.IsAny<DispatchJob>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Duplicate key violation"));

            var queue = new ReminderDispatchQueue(repository.Object);

            await queue.QueueAsync(BuildWorkItem(5, "2026-09-01T10:00:00Z", "PatientReminder"));

            repository.Verify(r => r.AddAsync(It.IsAny<DispatchJob>(), default), Times.Once);
        }

        private static ReminderDispatchWorkItem BuildWorkItem(int sessionId, string fechaHoraIso, string dispatchType)
            => new ReminderDispatchWorkItem
            {
                SessionId = sessionId,
                FechaHora = DateTime.Parse(fechaHoraIso, null, System.Globalization.DateTimeStyles.RoundtripKind),
                PacienteNombre = "Personal de Prueba",
                PacienteEmail = "p@test.com",
                PacienteTelefono = "5491100001111",
                ProfesionalNombre = "Gomez, Ana",
                TratamientoDescripcion = "Rehab",
                ConfirmUrl = $"https://localhost/confirm/{sessionId}",
                CancelUrl = $"https://localhost/cancel/{sessionId}",
                ChangedBy = "admin@kinegestion.com",
                EnqueuedAtUtc = DateTime.UtcNow,
                DispatchType = dispatchType
            };
    }
}