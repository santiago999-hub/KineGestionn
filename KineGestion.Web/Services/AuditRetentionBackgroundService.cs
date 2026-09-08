using KineGestion.Core.Interfaces;

namespace KineGestion.Web.Services
{
    public sealed class AuditRetentionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditRetentionBackgroundService> _logger;
        private readonly bool _enabled;
        private readonly int _retentionDays;
        private readonly int _startupDelayMs;
        private readonly int _repeatIntervalHours;
        private readonly int _batchSize;

        public AuditRetentionBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<AuditRetentionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool?>("Audit:RetentionEnabled") ?? true;
            _retentionDays = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Audit:RetentionDays",
                defaultValue: 180,
                min: 7,
                max: 3650);
            _startupDelayMs = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Audit:RetentionStartupDelayMs",
                defaultValue: 10000,
                min: 0,
                max: 60000);
            _repeatIntervalHours = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Audit:RetentionIntervalHours",
                defaultValue: 24,
                min: 1,
                max: 168);
            _batchSize = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Audit:RetentionBatchSize",
                defaultValue: 1000,
                min: 100,
                max: 10000);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Retención de auditoría deshabilitada por configuración.");
                return;
            }

            if (_startupDelayMs > 0)
            {
                try
                {
                    await Task.Delay(_startupDelayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ejecutando la limpieza de auditoría.");
                }

                await Task.Delay(TimeSpan.FromHours(_repeatIntervalHours), stoppingToken);
            }
        }

        private async Task PurgeOnceAsync(CancellationToken stoppingToken)
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(-_retentionDays);

            using var scope = _scopeFactory.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var deleted = await auditService.DeleteOlderThanAsync(cutoffUtc, _batchSize, stoppingToken);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Retención de auditoría: se purgaron {Deleted} registros anteriores a {Cutoff:o}.",
                    deleted,
                    cutoffUtc);
            }
        }
    }
}