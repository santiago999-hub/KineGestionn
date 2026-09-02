using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using Moq;
using Xunit;

namespace KineGestion.Tests
{
    public class EquipmentServiceTests
    {
        private readonly Mock<IEquipmentRepository> _repositoryMock;
        private readonly EquipmentService _service;

        public EquipmentServiceTests()
        {
            _repositoryMock = new Mock<IEquipmentRepository>();
            _service = new EquipmentService(_repositoryMock.Object);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenNameExistsInOffice()
        {
            var equipment = BuildEquipment();

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(equipment.Name, equipment.OfficeId, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(equipment));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipment>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenNameIsBlank()
        {
            var equipment = BuildEquipment();
            equipment.Name = "   ";

            var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(equipment));

            Assert.Equal(nameof(Equipment.Name), ex.PropertyName);
            _repositoryMock.Verify(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipment>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenNameIsUnique()
        {
            var equipment = BuildEquipment();

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(equipment.Name, equipment.OfficeId, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(equipment))
                .ReturnsAsync(equipment);

            var result = await _service.CreateAsync(equipment);

            Assert.Equal(equipment.Name, result.Name);
            _repositoryMock.Verify(r => r.AddAsync(equipment), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldTrimNameBeforePersist()
        {
            var equipment = BuildEquipment();
            equipment.Name = "  Camilla  ";

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync("Camilla", equipment.OfficeId, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Equipment>()))
                .ReturnsAsync((Equipment e) => e);

            var result = await _service.CreateAsync(equipment);

            Assert.Equal("Camilla", result.Name);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<Equipment>(e => e.Name == "Camilla")), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenAnotherEquipmentHasSameName()
        {
            var equipment = BuildEquipment();
            equipment.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(equipment.Name, equipment.OfficeId, equipment.Id))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(equipment));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Equipment>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ShouldPersist_WhenNameIsUnique()
        {
            var equipment = BuildEquipment();
            equipment.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(equipment.Name, equipment.OfficeId, equipment.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(equipment))
                .ReturnsAsync(equipment);

            var result = await _service.UpdateAsync(equipment);

            Assert.Equal(5, result.Id);
            _repositoryMock.Verify(r => r.UpdateAsync(equipment), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenEquipmentDoesNotExist()
        {
            _repositoryMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((Equipment?)null);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(99));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelete_WhenEquipmentExists()
        {
            var equipment = BuildEquipment();
            equipment.Id = 7;

            _repositoryMock
                .Setup(r => r.GetByIdAsync(7))
                .ReturnsAsync(equipment);

            _repositoryMock
                .Setup(r => r.DeleteAsync(7))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(7);

            _repositoryMock.Verify(r => r.DeleteAsync(7), Times.Once);
        }

        private static Equipment BuildEquipment()
        {
            return new Equipment
            {
                Id = 1,
                Name = "Camilla",
                OfficeId = 3
            };
        }
    }
}
