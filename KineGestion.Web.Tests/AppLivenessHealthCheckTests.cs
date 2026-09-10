using KineGestion.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KineGestion.Web.Tests
{
    public class AppLivenessHealthCheckTests
    {
        [Fact]
        public async Task CheckHealthAsync_ShouldReturnHealthy_WhenProcessIsAlive()
        {
            var check = new AppLivenessHealthCheck();

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldNotDependOnDatabase()
        {
            // El liveness no debe tocar SQL Server: es un chequeo de proceso, no de readiness.
            var check = new AppLivenessHealthCheck();

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Null(result.Exception);
        }
    }
}