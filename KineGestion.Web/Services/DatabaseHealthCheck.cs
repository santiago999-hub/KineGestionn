using KineGestion.Data.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KineGestion.Web.Services
{
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _dbContext;

        public DatabaseHealthCheck(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("Base de datos disponible.")
                    : HealthCheckResult.Unhealthy("No se pudo establecer conexión con la base de datos.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Error validando la base de datos.", ex);
            }
        }
    }
}