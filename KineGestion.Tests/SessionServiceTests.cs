using System;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using Moq;
using Xunit;

namespace KineGestion.Tests
{
    public class SessionServiceTests
    {
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly Mock<ITreatmentRepository> _treatmentRepositoryMock;
        private readonly SessionService _service;

        public SessionServiceTests()
        {
            QueryCache.ClearAll();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _treatmentRepositoryMock = new Mock<ITreatmentRepository>();
            _service = new SessionService(_sessionRepositoryMock.Object, _treatmentRepositoryMock.Object);
        }

        [Fact]
        public async Task GetPagedListForAdminAsync_ShouldCacheResultBetweenCalls()
        {
            var expected = (Items: (IEnumerable<KineGestion.Core.DTOs.SessionListDto>)new[]
            {
                new KineGestion.Core.DTOs.SessionListDto(1, DateTime.UtcNow, Core.SessionStatus.Pending, Core.PaymentStatus.Pending, 1, "Paciente", "Profesional", "Tratamiento", "Consultorio", false)
            }, TotalCount: 1);

            _sessionRepositoryMock
                .Setup(r => r.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null))
                .ReturnsAsync(expected);

            var first = await _service.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null);
            var second = await _service.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null);

            Assert.Equal(1, first.TotalCount);
            Assert.Equal(1, second.TotalCount);
            _sessionRepositoryMock.Verify(r => r.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenProfessionalHasConflict()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        [Fact]
        public async Task CreateAsync_ShouldUseConfiguredConflictWindow()
        {
            var session = BuildSession();
            var customWindow = 30;
            var serviceWithCustomWindow = new SessionService(
                _sessionRepositoryMock.Object,
                _treatmentRepositoryMock.Object,
                customWindow);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, customWindow, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => serviceWithCustomWindow.CreateAsync(session));

            _sessionRepositoryMock.Verify(
                r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, customWindow, null),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenTreatmentSessionLimitReached()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(10);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 10, Descripcion = "Plan" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        [Fact]
        public async Task UpdateAsync_ShouldRecalculateNroSesion_WhenTreatmentChanges()
        {
            var session = BuildSession();
            session.Id = 10;
            session.TreatmentId = 2;

            var original = BuildSession();
            original.Id = 10;
            original.TreatmentId = 1;
            original.NroSesionEnTratamiento = 5;

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(3);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 20, Descripcion = "Nuevo" });

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var updated = await _service.UpdateAsync(session);

            Assert.Equal(4, updated.NroSesionEnTratamiento);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenEvolutionIsLockedAndChanged()
        {
            var session = BuildSession();
            session.Id = 11;
            session.Evolution = "texto nuevo";

            var original = BuildSession();
            original.Id = 11;
            original.Evolution = "texto anterior";
            original.EvolutionLockedAt = DateTime.UtcNow.AddDays(-1);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(session));
        }

        // ─── CreateAsync — happy paths ────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_ShouldAssignNroSesion_WhenDataIsValid()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(3); // hay 3 sesiones → la nueva será la 4ª

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 10, Descripcion = "Plan" });

            _sessionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.CreateAsync(session);

            Assert.Equal(4, result.NroSesionEnTratamiento);
            _sessionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Session>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldAssignNroSesionOne_WhenTreatmentHasNoSessions()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(0); // primera sesión del tratamiento

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 5, Descripcion = "Inicio" });

            _sessionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.CreateAsync(session);

            Assert.Equal(1, result.NroSesionEnTratamiento);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenPatientExceedsSessionLimit()
        {
            // Esta validación no está en SessionService actualmente, pero si se agrega
            // en el futuro, este test documenta el comportamiento esperado.
            // Por ahora verifica que el límite del TRATAMIENTO es el punto de control.
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(5);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 5, Descripcion = "Completado" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        // ─── UpdateAsync — happy paths ────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ShouldLockEvolution_WhenSetForFirstTime()
        {
            var session = BuildSession();
            session.Id = 20;
            session.Evolution = "Primera evolución del paciente.";

            var original = BuildSession();
            original.Id = 20;
            original.Evolution = null;
            original.EvolutionLockedAt = null; // no estaba bloqueada

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.UpdateAsync(session);

            // Al escribir la evolución por primera vez, se debe bloquear automáticamente
            Assert.NotNull(result.EvolutionLockedAt);
        }

        [Fact]
        public async Task UpdateAsync_ShouldPreserveEvolutionLock_WhenEvolutionIsUnchanged()
        {
            var lockedAt = DateTime.UtcNow.AddDays(-2);
            var session = BuildSession();
            session.Id = 21;
            session.Evolution = "Evolución firmada.";

            var original = BuildSession();
            original.Id = 21;
            original.Evolution = "Evolución firmada."; // mismo texto → no cambia
            original.EvolutionLockedAt = lockedAt;

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.UpdateAsync(session);

            // La fecha de bloqueo original debe preservarse sin cambios
            Assert.Equal(lockedAt, result.EvolutionLockedAt);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenNewTreatmentIsAlsoFull()
        {
            var session = BuildSession();
            session.Id = 22;
            session.TreatmentId = 99; // cambia a tratamiento diferente

            var original = BuildSession();
            original.Id = 22;
            original.TreatmentId = 1; // tratamiento original

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(99))
                .ReturnsAsync(8);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync(new Treatment { Id = 99, CantidadSesionesTotales = 8, Descripcion = "Lleno" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(session));
        }

        // ─── DeleteAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ShouldCallRepositoryDelete()
        {
            _sessionRepositoryMock
                .Setup(r => r.DeleteAsync(5))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(5);

            _sessionRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
        }

        private static Session BuildSession()
        {
            return new Session
            {
                Id = 1,
                FechaHora = DateTime.UtcNow,
                ProfessionalId = 3,
                PatientId = 2,
                TreatmentId = 1,
                NroSesionEnTratamiento = 1,
                Status = Core.SessionStatus.Pending,
                PaymentStatus = Core.PaymentStatus.Pending
            };
        }
    }
}
