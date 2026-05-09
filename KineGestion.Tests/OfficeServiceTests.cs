using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using Moq;
using Xunit;

namespace KineGestion.Tests
{
    public class OfficeServiceTests
    {
        private readonly Mock<IOfficeRepository> _repositoryMock;
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly OfficeService _service;

        public OfficeServiceTests()
        {
            _repositoryMock = new Mock<IOfficeRepository>();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _service = new OfficeService(_repositoryMock.Object, _sessionRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenNameAlreadyExists()
        {
            var office = BuildOffice();

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(office));
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Office>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenNameIsBlank()
        {
            var office = BuildOffice();
            office.Name = "   ";

            var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(office));

            Assert.Equal(nameof(Office.Name), ex.PropertyName);
            _repositoryMock.Verify(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Office>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenNameIsUnique()
        {
            var office = BuildOffice();

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(office))
                .ReturnsAsync(office);

            var result = await _service.CreateAsync(office);

            Assert.Equal(office.Name, result.Name);
            _repositoryMock.Verify(r => r.AddAsync(office), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldValidateUniquenessExcludingSelf()
        {
            var office = BuildOffice();
            office.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, office.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(office))
                .ReturnsAsync(office);

            var result = await _service.UpdateAsync(office);

            Assert.Equal(5, result.Id);
            _repositoryMock.Verify(r => r.ExistsByNameAsync(office.Name, office.Id), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenAnotherOfficeHasSameName()
        {
            var office = BuildOffice();
            office.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, office.Id))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(office));
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Office>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ShouldPersist_WhenNameIsUnique()
        {
            var office = BuildOffice();
            office.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, office.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(office))
                .ReturnsAsync(office);

            var result = await _service.UpdateAsync(office);

            Assert.Equal(office.Name, result.Name);
            _repositoryMock.Verify(r => r.UpdateAsync(office), Times.Once);
        }

        private static Office BuildOffice()
        {
            return new Office
            {
                Id = 1,
                Name = "Consultorio A",
                IsActive = true
            };
        }

        [Fact]
        public async Task DeleteAsync_ShouldThrow_WhenOfficeHasSessions()
        {
            _sessionRepositoryMock
                .Setup(r => r.CountByOfficeIdAsync(1))
                .ReturnsAsync(2);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.DeleteAsync(1));
            _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelete_WhenOfficeHasNoSessions()
        {
            _sessionRepositoryMock
                .Setup(r => r.CountByOfficeIdAsync(1))
                .ReturnsAsync(0);

            _repositoryMock
                .Setup(r => r.DeleteAsync(1))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(1);

            _repositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
        }
    }
}
