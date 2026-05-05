using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using Moq;
using Xunit;

namespace KineGestion.Tests
{
    public class TreatmentServiceTests
    {
        private readonly Mock<ITreatmentRepository> _repositoryMock;
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly TreatmentService _service;

        public TreatmentServiceTests()
        {
            _repositoryMock = new Mock<ITreatmentRepository>();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _service = new TreatmentService(_repositoryMock.Object, _sessionRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenCantidadSesionesIsZero()
        {
            var treatment = BuildTreatment();
            treatment.CantidadSesionesTotales = 0;

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(treatment));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Treatment>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenFechaInicioIsDefault()
        {
            var treatment = BuildTreatment();
            treatment.FechaInicio = default;

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(treatment));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Treatment>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenValid()
        {
            var treatment = BuildTreatment();

            _repositoryMock
                .Setup(r => r.AddAsync(treatment))
                .ReturnsAsync(treatment);

            var result = await _service.CreateAsync(treatment);

            Assert.Equal(treatment.Descripcion, result.Descripcion);
            _repositoryMock.Verify(r => r.AddAsync(treatment), Times.Once);
        }

        [Fact]
        public async Task GetByPatientIdAsync_ShouldDelegateToRepository()
        {
            var expected = new List<Treatment>
            {
                new() { Id = 1, PatientId = 4, Descripcion = "Plan", CantidadSesionesTotales = 10 }
            };

            _repositoryMock
                .Setup(r => r.GetByPatientIdAsync(4))
                .ReturnsAsync(expected);

            var result = await _service.GetByPatientIdAsync(4);

            Assert.Single(result);
            _repositoryMock.Verify(r => r.GetByPatientIdAsync(4), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenCantidadSesionesIsZero()
        {
            var treatment = BuildTreatment();
            treatment.CantidadSesionesTotales = 0;

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(treatment));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Treatment>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenTreatmentHasSessions()
        {
            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(9))
                .ReturnsAsync(5);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(9));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelegateToRepository()
        {
            _repositoryMock
                .Setup(r => r.DeleteAsync(9))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(9);

            _repositoryMock.Verify(r => r.DeleteAsync(9), Times.Once);
        }

        // ─── UpdateAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ShouldPersist_WhenValid()
        {
            var treatment = BuildTreatment();

            _repositoryMock
                .Setup(r => r.UpdateAsync(treatment))
                .ReturnsAsync(treatment);

            var result = await _service.UpdateAsync(treatment);

            Assert.Equal(treatment.Descripcion, result.Descripcion);
            _repositoryMock.Verify(r => r.UpdateAsync(treatment), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenFechaInicioIsDefault()
        {
            var treatment = BuildTreatment();
            treatment.FechaInicio = default; // misma validación que en Create

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(treatment));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Treatment>()), Times.Never);
        }

        private static Treatment BuildTreatment()
        {
            return new Treatment
            {
                Id = 1,
                PatientId = 4,
                Descripcion = "Plan de rehabilitacion",
                CantidadSesionesTotales = 10,
                FechaInicio = DateTime.Today
            };
        }
    }
}
