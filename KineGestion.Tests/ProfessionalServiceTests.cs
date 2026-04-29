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
        private readonly ProfessionalService _service;

        public ProfessionalServiceTests()
        {
            _repositoryMock = new Mock<IProfessionalRepository>();
            _service = new ProfessionalService(_repositoryMock.Object);
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
    }
}
