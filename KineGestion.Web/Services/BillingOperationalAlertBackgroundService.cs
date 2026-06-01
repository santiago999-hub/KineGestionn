namespace KineGestion.Web.Services
{
    public sealed class BillingOperationalAlertBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BillingOperationalAlertBackgroundService> _logger;
        private readonly bool _enabled;
        private readonly int _startupDelayMs;
        private readonly int _repeatIntervalMinutes;

        public BillingOperationalAlertBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<BillingOperationalAlertBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool?>("Reminders:OperationalAlerts:Enabled") ?? true;
            _startupDelayMs = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Reminders:OperationalAlerts:StartupDelayMs",
                defaultValue: 5000,
                min: 0,
                max: 60000);
            _repeatIntervalMinutes = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Reminders:OperationalAlerts:BackgroundCheckIntervalMinutes",
                defaultValue: 60,
                min: 5,
                max: 1440);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Chequeo automático de alertas operativas deshabilitado por configuración.");
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
                    using var scope = _scopeFactory.CreateScope();
                    var alertService = scope.ServiceProvider.GetRequiredService<IBillingOperationalAlertService>();
                    var result = await alertService.QueueAlertIfNeededAsync("system", DateTime.UtcNow, stoppingToken);

                    if (result.Queued)
                        _logger.LogWarning("{Message}", result.Message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ejecutando chequeo automático de alertas operativas.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_repeatIntervalMinutes), stoppingToken);
            }
        }
    }
}