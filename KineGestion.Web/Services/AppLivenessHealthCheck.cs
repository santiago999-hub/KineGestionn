using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KineGestion.Web.Services
{
    /// <summary>
    /// Liveness del proceso: representa "la app responde y el pipeline de health checks
    /// está en funcionamiento". No depende de SQL Server ni de la cola (eso es readiness).
    /// Si el proceso queda colgado, el probe HTTP deja de responder y el orquestador
    /// (Docker/Kubernetes) reinicia el contenedor.
    /// </summary>
    public sealed class AppLivenessHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Proceso activo."));
        }
    }
}