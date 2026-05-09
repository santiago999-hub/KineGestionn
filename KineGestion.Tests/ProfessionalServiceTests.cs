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
    public class ProfessionalServiceTests
    {
        private readonly Mock<IProfessionalRepository> _repositoryMock;
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly ProfessionalService _service;

        public ProfessionalServiceTests()
        {
            _repositoryMock = new Mock<IProfessionalRepository>();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _service = new ProfessionalService(_repositoryMock.Object, _sessionRepositoryMock.Object);
        }

        [Fact]
        public async Task GetActiveProfessionalsAsync_ShouldCallRepositoryGetActivosAsync()
        {
            var expected = new List<Professional>
            {
                new() { Id = 1, Nombre = "Laura", Apellido = "Diaz", Matricula = "MP123" }
            };

            _repositoryMock
                .Setup(r => r.GetActivosAsync())
                .ReturnsAsync(expected);

            var result = await _service.GetActiveProfessionalsAsync();

            Assert.Single(result);
            _repositoryMock.Verify(r => r.GetActivosAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenMatriculaAlreadyExists()
        {
            var professional = BuildProfessional();

            _repositoryMock
                .Setup(r => r.ExistsByMatriculaAsync(professional.Matricula, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(professional));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Professional>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenMatriculaIsBlank()
        {
            var professional = BuildProfessional();
            professional.Matricula = "   ";

            var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(professional));

            Assert.Equal(nameof(Professional.Matricula), ex.PropertyName);
            _repositoryMock.Verify(r => r.ExistsByMatriculaAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Professional>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldForceIsActivoTrue_AndPersist()
        {
            var professional = BuildProfessional();
            professional.IsActivo = false;

            _repositoryMock
                .Setup(r => r.ExistsByMatriculaAsync(professional.Matricula, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Professional>()))
                .ReturnsAsync((Professional value) => value);

            var result = await _service.CreateAsync(professional);

            Assert.True(result.IsActivo);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<Professional>(p => p.IsActivo)), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldValidateUniquenessUsingExcludeId()
        {
            var professional = BuildProfessional();
            professional.Id = 7;

            _repositoryMock
                .Setup(r => r.ExistsByMatriculaAsync(professional.Matricula, professional.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(professional))
                .ReturnsAsync(professional);

            var result = await _service.UpdateAsync(professional);

            Assert.Equal(professional.Id, result.Id);
            _repositoryMock.Verify(r => r.ExistsByMatriculaAsync(professional.Matricula, professional.Id), Times.Once);
            _repositoryMock.Verify(r => r.UpdateAsync(professional), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenMatriculaAlreadyUsedByOther()
        {
            var professional = BuildProfessional();
            professional.Id = 7;

            _repositoryMock
                .Setup(r => r.ExistsByMatriculaAsync(professional.Matricula, professional.Id))
                .ReturnsAsync(true); // otra matrícula registrada con el mismo número

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(professional));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Professional>()), Times.Never);
        }

        private static Professional BuildProfessional()
        {
            return new Professional
            {
                Id = 1,
                Nombre = "Laura",
                Apellido = "Diaz",
                Matricula = "MP123",
                Especialidad = "Deportiva",
                IsActivo = true
            };
        }

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenProfessionalHasSessions()
        {
            _sessionRepositoryMock
                .Setup(r => r.CountByProfessionalIdAsync(1))
                .ReturnsAsync(4);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(1));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelete_WhenProfessionalHasNoSessions()
        {
            _sessionRepositoryMock
                .Setup(r => r.CountByProfessionalIdAsync(1))
                .ReturnsAsync(0);

            _repositoryMock
                .Setup(r => r.DeleteAsync(1))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(1);

            _repositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
        }
    }
}
