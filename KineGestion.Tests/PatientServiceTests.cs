using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
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
        private readonly Mock<ITreatmentRepository> _treatmentRepositoryMock;
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly PatientService _service;

        public PatientServiceTests()
        {
            QueryCache.InvalidatePrefix("patients:");
            _repositoryMock = new Mock<IPatientRepository>();
            _treatmentRepositoryMock = new Mock<ITreatmentRepository>();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _service = new PatientService(
                _repositoryMock.Object,
                _treatmentRepositoryMock.Object,
                _sessionRepositoryMock.Object);
        }

        [Fact]
#pragma warning disable CS0618 // Test intencional: verifica que el método obsoleto delega al repositorio correctamente.
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
#pragma warning restore CS0618

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
        public async Task CreateAsync_ShouldThrow_WhenDniIsBlank()
        {
            var patient = BuildPatient();
            patient.DNI = "   ";

            var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(patient));

            Assert.Equal(nameof(Patient.DNI), ex.PropertyName);
            _repositoryMock.Verify(r => r.ExistsByDniAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
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

        [Fact]
        public async Task GetForSelectAsync_ShouldCacheResultBetweenCalls()
        {
            var expected = new List<PatientSelectDto>
            {
                new(1, "Ana", "Perez", "30111222")
            };

            _repositoryMock
                .Setup(r => r.GetForSelectAsync())
                .ReturnsAsync(expected);

            var first = await _service.GetForSelectAsync();
            var second = await _service.GetForSelectAsync();

            Assert.Single(first);
            Assert.Single(second);
            _repositoryMock.Verify(r => r.GetForSelectAsync(), Times.Once);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldCacheResultBetweenCalls()
        {
            var expected = (Patients: (IEnumerable<Patient>)new[] { BuildPatient() }, TotalCount: 1);

            _repositoryMock
                .Setup(r => r.GetPagedAsync(1, 10, null))
                .ReturnsAsync(expected);

            var first = await _service.GetPagedAsync(1, 10, null);
            var second = await _service.GetPagedAsync(1, 10, null);

            Assert.Equal(1, first.TotalCount);
            Assert.Equal(1, second.TotalCount);
            _repositoryMock.Verify(r => r.GetPagedAsync(1, 10, null), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldInvalidateCachedSelects()
        {
            var patient = BuildPatient();

            _repositoryMock
                .Setup(r => r.GetForSelectAsync())
                .ReturnsAsync(new List<PatientSelectDto>
                {
                    new(1, "Ana", "Perez", "30111222")
                });

            _repositoryMock
                .Setup(r => r.ExistsByDniAsync(patient.DNI, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(patient))
                .ReturnsAsync(patient);

            await _service.GetForSelectAsync();
            await _service.CreateAsync(patient);
            await _service.GetForSelectAsync();

            _repositoryMock.Verify(r => r.GetForSelectAsync(), Times.Exactly(2));
            _repositoryMock.Verify(r => r.AddAsync(patient), Times.Once);
        }

        // ─── UpdateAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ShouldPersist_WhenValid()
        {
            var patient = BuildPatient();

            _repositoryMock
                .Setup(r => r.ExistsByDniAsync(patient.DNI, patient.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(patient))
                .ReturnsAsync(patient);

            var result = await _service.UpdateAsync(patient);

            Assert.Equal(patient.DNI, result.DNI);
            _repositoryMock.Verify(r => r.UpdateAsync(patient), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenDniAlreadyUsedByOther()
        {
            var patient = BuildPatient();

            _repositoryMock
                .Setup(r => r.ExistsByDniAsync(patient.DNI, patient.Id))
                .ReturnsAsync(true); // otro paciente ya tiene ese DNI

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(patient));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Patient>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenBirthDateIsToday()
        {
            var patient = BuildPatient();
            patient.FechaNacimiento = DateTime.Today; // fecha inválida en edición también

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(patient));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Patient>()), Times.Never);
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

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenPatientHasTreatments()
        {
            _treatmentRepositoryMock
                .Setup(r => r.CountByPatientIdAsync(1))
                .ReturnsAsync(3);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(1));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenPatientHasSessionsOnly()
        {
            _treatmentRepositoryMock
                .Setup(r => r.CountByPatientIdAsync(1))
                .ReturnsAsync(0);

            _sessionRepositoryMock
                .Setup(r => r.CountByPatientIdAsync(1))
                .ReturnsAsync(2);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(1));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelete_WhenPatientHasNoDependencies()
        {
            _treatmentRepositoryMock
                .Setup(r => r.CountByPatientIdAsync(1))
                .ReturnsAsync(0);

            _sessionRepositoryMock
                .Setup(r => r.CountByPatientIdAsync(1))
                .ReturnsAsync(0);

            _repositoryMock
                .Setup(r => r.DeleteAsync(1))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(1);

            _repositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task TryValidateObraSocial_ShouldReturnTrue_WhenObraSocialIsNull()
        {
            // Arrange
            string? obraSocial = null;
            string? errorMessage;

            // Act
            var result = PatientService.TryValidateObraSocial(obraSocial, out errorMessage);

            // Assert
            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public async Task TryValidateObraSocial_ShouldReturnTrue_WhenObraSocialIsKnownValue()
        {
            // Arrange
            var knownValues = new[] { "OSDE", "PAMI", "IOMA", "Swiss", "Particular", "OTRO" };
            string? errorMessage;

            // Act & Assert
            foreach (var value in knownValues)
            {
                var result = PatientService.TryValidateObraSocial(value, out errorMessage);
                Assert.True(result, $"Debería ser válido el valor: {value}");
                Assert.Null(errorMessage); // No debería haber error para el valor: {value}");
            }
        }

        [Fact]
        public async Task TryValidateObraSocial_ShouldReturnTrue_WhenObraSocialIsUnrecognizedValue()
        {
            // Arrange
            var unrecognizedValues = new[] { "ObraSocialX", "ASD", "", "  " };
            string? errorMessage;

            // Act & Assert
            foreach (var value in unrecognizedValues)
            {
                var result = PatientService.TryValidateObraSocial(value, out errorMessage);
                // Debería retornar true (backward compatibility) pero con mensaje de advertencia para valores no nulos
                Assert.True(result, $"Debería aceptar el valor para backward compatibility: '{value}'");
                // Valores vacíos o nulos no deberían tener mensaje de error
                if (string.IsNullOrWhiteSpace(value))
                    Assert.Null(errorMessage);
                else
                    // Valores no reconocidos sí tendrán mensaje de advertencia
                    Assert.NotNull(errorMessage);
            }
        }

        [Fact]
        public async Task CreateAsync_ShouldNotThrow_WhenObraSocialIsValid()
        {
            // Arrange
            var patient = BuildPatient(); // ObraSocial = "OSDE" por defecto, FechaNacimiento en el pasado

            // Act
            var exception = await Record.ExceptionAsync(() => _service.CreateAsync(patient));

            // Assert
            Assert.Null(exception); // No debería lanzar excepción para ObraSocial válida y fecha correcta
        }

        [Fact]
        public async Task UpdateAsync_ShouldNotThrow_WhenObraSocialIsValid()
        {
            // Arrange
            var patient = BuildPatient(); // ObraSocial = "OSDE" por defecto

            // Act
            var exception = await Record.ExceptionAsync(() => _service.UpdateAsync(patient));

            // Assert
            Assert.Null(exception); // No debería lanzar excepción para ObraSocial válida
        }
    }
}
