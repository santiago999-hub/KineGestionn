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
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _treatmentRepositoryMock = new Mock<ITreatmentRepository>();
            _service = new SessionService(_sessionRepositoryMock.Object, _treatmentRepositoryMock.Object);
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
