using KineGestion.Core;
using KineGestion.Core.Interfaces;

namespace KineGestion.Web.Services
{
    public sealed class CacheWarmupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CacheWarmupBackgroundService> _logger;
        private readonly bool _enabled;
        private readonly int _startupDelayMs;
        private readonly int _repeatIntervalSeconds;

        public CacheWarmupBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<CacheWarmupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool?>("Performance:Warmup:Enabled") ?? true;
            _startupDelayMs = Math.Clamp(configuration.GetValue<int?>("Performance:Warmup:StartupDelayMs") ?? 1500, 0, 60000);
            _repeatIntervalSeconds = Math.Max(0, configuration.GetValue<int?>("Performance:Warmup:RepeatIntervalSeconds") ?? 0);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Warmup de caché deshabilitado por configuración.");
                return;
            }

            if (_startupDelayMs > 0)
                await Task.Delay(_startupDelayMs, stoppingToken);

            do
            {
                await WarmupOnceAsync(stoppingToken);

                if (_repeatIntervalSeconds <= 0)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(_repeatIntervalSeconds), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }

        private async Task WarmupOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
                var professionalService = scope.ServiceProvider.GetRequiredService<IProfessionalService>();
                var treatmentService = scope.ServiceProvider.GetRequiredService<ITreatmentService>();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

                var nowUtc = DateTime.UtcNow;
                var rangeFrom = nowUtc.Date.AddDays(-30);
                var rangeTo = nowUtc.Date.AddDays(1);

                var tasks = new Task[]
                {
                    SafeRunAsync(() => patientService.CountActiveAsync(), "patients:count:active"),
                    SafeRunAsync(() => professionalService.CountActiveAsync(), "professionals:count:active"),
                    SafeRunAsync(() => treatmentService.CountAsync(), "treatments:count:all"),
                    SafeRunAsync(() => sessionService.CountAsync(), "sessions:count:all"),
                    SafeRunAsync(() => sessionService.CountTodayAsync(nowUtc), "sessions:count:today"),
                    SafeRunAsync(() => sessionService.CountByStatusOnDateAsync(SessionStatus.Completed, nowUtc), "sessions:count:status:completed:today"),
                    SafeRunAsync(() => sessionService.CountByPaymentStatusAsync(PaymentStatus.Pending), "sessions:count:payment:pending"),
                    SafeRunAsync(() => sessionService.CountByStatusAsync(SessionStatus.Pending), "sessions:count:status:pending"),
                    SafeRunAsync(() => sessionService.CountInRangeAsync(rangeFrom, rangeTo), "sessions:count:range:last30"),
                    SafeRunAsync(() => sessionService.CountByPaymentStatusInRangeAsync(PaymentStatus.Paid, rangeFrom, rangeTo), "sessions:count:payment:paid:last30"),
                    SafeRunAsync(() => sessionService.CountByStatusInRangeAsync(SessionStatus.Canceled, rangeFrom, rangeTo), "sessions:count:status:canceled:last30"),
                    SafeRunAsync(() => sessionService.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, "fecha", "desc"), "sessions:admin:paged:first")
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("Warmup de caché completado correctamente.");
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warmup de caché finalizó con errores no críticos.");
            }

            async Task SafeRunAsync<T>(Func<Task<T>> action, string metricName)
            {
                try
                {
                    _ = await action();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Warmup omitido para {MetricName}", metricName);
                }
            }
        }
    }
}