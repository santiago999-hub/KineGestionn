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
            QueryCache.ClearAll();
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
        public async Task CreateAsync_ShouldTrimNameBeforeValidationAndPersist()
        {
            var office = BuildOffice();
            office.Name = "  Consultorio Norte  ";

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync("Consultorio Norte", null))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Office>()))
                .ReturnsAsync((Office o) => o);

            var result = await _service.CreateAsync(office);

            Assert.Equal("Consultorio Norte", result.Name);
            _repositoryMock.Verify(r => r.ExistsByNameAsync("Consultorio Norte", null), Times.Once);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<Office>(o => o.Name == "Consultorio Norte")), Times.Once);
        }

        [Fact]
        public async Task GetActiveAsync_ShouldCacheResultBetweenCalls()
        {
            var expected = new[] { BuildOffice() };

            _repositoryMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(expected);

            var first = await _service.GetActiveAsync();
            var second = await _service.GetActiveAsync();

            Assert.Single(first);
            Assert.Single(second);
            _repositoryMock.Verify(r => r.GetActiveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldInvalidateCachedActiveList()
        {
            var office = BuildOffice();
            office.Id = 5;

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync(office.Name, office.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .SetupSequence(r => r.GetActiveAsync())
                .ReturnsAsync(new[] { BuildOffice() })
                .ReturnsAsync(new[] { office });

            _repositoryMock
                .Setup(r => r.UpdateAsync(office))
                .ReturnsAsync(office);

            await _service.GetActiveAsync();
            await _service.UpdateAsync(office);
            await _service.GetActiveAsync();

            _repositoryMock.Verify(r => r.GetActiveAsync(), Times.Exactly(2));
            _repositoryMock.Verify(r => r.UpdateAsync(office), Times.Once);
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

        [Fact]
        public async Task UpdateAsync_ShouldTrimNameBeforeValidationAndPersist()
        {
            var office = BuildOffice();
            office.Id = 7;
            office.Name = "  Consultorio Centro  ";

            _repositoryMock
                .Setup(r => r.ExistsByNameAsync("Consultorio Centro", office.Id))
                .ReturnsAsync(false);

            _repositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Office>()))
                .ReturnsAsync((Office o) => o);

            var result = await _service.UpdateAsync(office);

            Assert.Equal("Consultorio Centro", result.Name);
            _repositoryMock.Verify(r => r.ExistsByNameAsync("Consultorio Centro", office.Id), Times.Once);
            _repositoryMock.Verify(r => r.UpdateAsync(It.Is<Office>(o => o.Name == "Consultorio Centro" && o.Id == office.Id)), Times.Once);
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
