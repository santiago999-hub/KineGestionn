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
    public class PatientServiceTests
    {
        private readonly Mock<IPatientRepository> _repositoryMock;
        private readonly PatientService _service;

        public PatientServiceTests()
        {
            _repositoryMock = new Mock<IPatientRepository>();
            _service = new PatientService(_repositoryMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldCallRepositoryGetAllAsync()
        {
            var expected = new List<Patient>
            {
                new() { Id = 1, Nombre = "Ana", Apellido = "Perez", DNI = "30111222", FechaNacimiento = DateTime.Today.AddYears(-30), ObraSocial = "OSDE" },
                new() { Id = 2, Nombre = "Luis", Apellido = "Gomez", DNI = "28999111", FechaNacimiento = DateTime.Today.AddYears(-25), ObraSocial = "Swiss" }
            };

            _repositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync();

            Assert.Equal(2, ((List<Patient>)result).Count);
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
            _repositoryMock.Verify(r => r.GetActivosAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenBirthDateIsToday()
        {
            var patient = BuildPatient();
            patient.FechaNacimiento = DateTime.Today;

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(patient));
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenDniAlreadyExists()
        {
            var patient = BuildPatient();

            _repositoryMock
                .Setup(r => r.ExistsByDniAsync(patient.DNI, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(patient));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Patient>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenValid()
        {
            var patient = BuildPatient();

            _repositoryMock
                .Setup(r => r.ExistsByDniAsync(patient.DNI, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(patient))
                .ReturnsAsync(patient);

            var result = await _service.CreateAsync(patient);

            Assert.Equal(patient.DNI, result.DNI);
            _repositoryMock.Verify(r => r.AddAsync(patient), Times.Once);
        }

        private static Patient BuildPatient()
        {
            return new Patient
            {
                Id = 1,
                Nombre = "Ana",
                Apellido = "Perez",
                DNI = "30111222",
                FechaNacimiento = DateTime.Today.AddYears(-28),
                ObraSocial = "OSDE",
                IsActivo = true
            };
        }
    }
}
