using KineGestion.Web.Models.ViewModels;
using KineGestion.Core;

namespace KineGestion.Web.Tests
{
    public class AuditIndexViewModelTests
    {
        [Fact]
        public void TotalPages_ShouldReturnOne_WhenTotalCountIsZero()
        {
            var model = new AuditIndexViewModel
            {
                TotalCount = 0,
                PageSize = 10
            };

            Assert.Equal(1, model.TotalPages);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void TotalPages_ShouldReturnOne_WhenPageSizeIsInvalid(int pageSize)
        {
            var model = new AuditIndexViewModel
            {
                TotalCount = 100,
                PageSize = pageSize
            };

            Assert.Equal(1, model.TotalPages);
        }

        [Fact]
        public void TotalPages_ShouldCalculateCeiling_WhenValuesAreValid()
        {
            var model = new AuditIndexViewModel
            {
                TotalCount = 25,
                PageSize = 10
            };

            Assert.Equal(3, model.TotalPages);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetActionLabel_ShouldReturnEmpty_WhenInputIsNullOrWhitespace(string? value)
        {
            var result = AuditIndexViewModel.GetActionLabel(value);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetActionLabel_ShouldReturnLocalizedValue_WhenInputIsKnownAction()
        {
            var result = AuditIndexViewModel.GetActionLabel("update");
            var expected = AuditIndexViewModel.GetActionLabel(AuditActionType.Update);

            Assert.False(string.IsNullOrWhiteSpace(result));
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetActionLabel_ShouldReturnOriginalValue_WhenInputIsUnknownAction()
        {
            const string value = "UnknownAction";

            var result = AuditIndexViewModel.GetActionLabel(value);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetEntityLabel_ShouldReturnEmpty_WhenInputIsNullOrWhitespace(string? value)
        {
            var result = AuditIndexViewModel.GetEntityLabel(value);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetEntityLabel_ShouldReturnLocalizedValue_WhenInputIsKnownEntity()
        {
            var result = AuditIndexViewModel.GetEntityLabel("Patient");

            Assert.False(string.IsNullOrWhiteSpace(result));
        }

        [Fact]
        public void GetEntityLabel_ShouldReturnOriginalValue_WhenInputIsUnknownEntity()
        {
            const string value = "UnknownEntity";

            var result = AuditIndexViewModel.GetEntityLabel(value);

            Assert.Equal(value, result);
        }
    }
}
