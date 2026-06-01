using KineGestion.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace KineGestion.Web.Tests
{
    public class OperationalConfigTests
    {
        [Fact]
        public void ReadBoundedInt_ShouldReturnDefault_WhenValueIsMissing()
        {
            var configuration = new ConfigurationBuilder().Build();

            var result = OperationalConfig.ReadBoundedInt(
                configuration,
                NullLogger.Instance,
                "Performance:Warmup:StartupDelayMs",
                defaultValue: 1500,
                min: 0,
                max: 60000);

            Assert.Equal(1500, result);
        }

        [Fact]
        public void ReadBoundedInt_ShouldClamp_WhenOutOfRange()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Performance:Warmup:OperationTimeoutSeconds"] = "999"
                })
                .Build();

            var result = OperationalConfig.ReadBoundedInt(
                configuration,
                NullLogger.Instance,
                "Performance:Warmup:OperationTimeoutSeconds",
                defaultValue: 10,
                min: 2,
                max: 120);

            Assert.Equal(120, result);
        }

        [Fact]
        public void ReadDistinctHourWindows_ShouldFallback_WhenNoValidValues()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Reminders:OperationalWindowsHours"] = "abc,-1,200"
                })
                .Build();

            var result = OperationalConfig.ReadDistinctHourWindows(
                configuration,
                NullLogger.Instance,
                "Reminders:OperationalWindowsHours",
                fallback: new[] { 24, 3 },
                min: 1,
                max: 168);

            Assert.Equal(new[] { 24, 3 }, result);
        }

        [Fact]
        public void ReadDistinctHourWindows_ShouldNormalizeDistinctAndSortDesc()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Reminders:OperationalWindowsHours"] = "3,24,3,12"
                })
                .Build();

            var result = OperationalConfig.ReadDistinctHourWindows(
                configuration,
                NullLogger.Instance,
                "Reminders:OperationalWindowsHours",
                fallback: new[] { 24, 3 },
                min: 1,
                max: 168);

            Assert.Equal(new[] { 24, 12, 3 }, result);
        }
    }
}
