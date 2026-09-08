using System;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KineGestion.Web.Services
{
    /// <summary>
    /// Marca /health/ready como Degraded cuando la cola de despachos tiene jobs
    /// pendientes estancados (el worker no los está procesando).
    /// </summary>
    public sealed class DispatchQueueHealthCheck : IHealthCheck
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public DispatchQueueHealthCheck(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var thresholdMinutes = _configuration.GetValue<int?>("DispatchQueue:StuckAlertThresholdMinutes") ?? 30;
                if (thresholdMinutes is < 5 or > 1440)
                    thresholdMinutes = 30;

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDispatchJobRepository>();

                var stats = await repository.GetStatsAsync(
                    DateTime.UtcNow.AddMinutes(-thresholdMinutes),
                    cancellationToken);

                if (stats.StuckCount > 0)
                {
                    return HealthCheckResult.Degraded(
                        $"Cola de despachos: {stats.StuckCount} jobs pendientes estancados (> {thresholdMinutes} min).");
                }

                return HealthCheckResult.Healthy("Cola de despachos operativa.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Error verificando la cola de despachos.", ex);
            }
        }
    }
}