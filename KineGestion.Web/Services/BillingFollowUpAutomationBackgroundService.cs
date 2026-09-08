namespace KineGestion.Web.Services
{
    /// <summary>
    /// Dispara la automatización D+1 de cobranza en segundo plano con la
    /// cadencia configurada. La escalada de niveles (suave/recordatorio/firme)
    /// evita reenviar al mismo paciente dentro del rango ya notificado.
    /// </summary>
    public sealed class BillingFollowUpAutomationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BillingFollowUpAutomationBackgroundService> _logger;
        private readonly bool _enabled;
        private readonly int _startupDelaySeconds;
        private readonly int _repeatIntervalMinutes;

        public BillingFollowUpAutomationBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<BillingFollowUpAutomationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool?>("BillingFollowUp:Automation:Enabled") ?? true;
            _startupDelaySeconds = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "BillingFollowUp:Automation:StartupDelaySeconds",
                defaultValue: 15,
                min: 0,
                max: 600);
            _repeatIntervalMinutes = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "BillingFollowUp:Automation:IntervalMinutes",
                defaultValue: 360,
                min: 5,
                max: 2880);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Automatización D+1 de cobranza deshabilitada por configuración.");
                return;
            }

            if (_startupDelaySeconds > 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_startupDelaySeconds), stoppingToken);
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
                    var automationService = scope.ServiceProvider.GetRequiredService<IBillingFollowUpAutomationService>();
                    var result = await automationService.RunAsync(DateTime.UtcNow, stoppingToken);

                    if (result.Queued > 0)
                        _logger.LogInformation("Automatización D+1: {Summary}", result.ToString());
                    else
                        _logger.LogDebug("Automatización D+1 sin envíos nuevos: {Summary}", result.ToString());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ejecutando automatización D+1 de cobranza.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_repeatIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}