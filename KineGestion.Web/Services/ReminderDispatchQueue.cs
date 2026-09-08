using System.Globalization;
using System.Text;
using System.Text.Json;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Web.Services
{
    public interface IReminderDispatchQueue
    {
        ValueTask QueueAsync(ReminderDispatchWorkItem workItem, CancellationToken cancellationToken = default);
    }

    public sealed class ReminderDispatchWorkItem
    {
        public int SessionId { get; set; }
        public DateTime FechaHora { get; set; }
        public string PacienteNombre { get; set; } = string.Empty;
        public string? PacienteEmail { get; set; }
        public string? PacienteTelefono { get; set; }
        public string ProfesionalNombre { get; set; } = string.Empty;
        public string? TratamientoDescripcion { get; set; }
        public string ConfirmUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string? ChangedBy { get; set; }
        public DateTime EnqueuedAtUtc { get; set; }
        public string DispatchType { get; set; } = "PatientReminder";
        public string? EmailSubjectOverride { get; set; }
        public string? EmailBodyOverride { get; set; }
        public string? WhatsAppBodyOverride { get; set; }
        public string AuditEntityName { get; set; } = "ReminderDispatch";
        public string? AuditEntityId { get; set; }
    }

    /// <summary>
    /// Cola durable (outbox) en la base: cada envío se persiste antes de responder al
    /// productor, de modo que un reinicio del proceso no pierde recordatorios.
    /// Deduplica envíos abiertos idénticos (doble click, productores concurrentes).
    /// </summary>
    public sealed class ReminderDispatchQueue : IReminderDispatchQueue
    {
        private readonly IDispatchJobRepository _repository;

        public ReminderDispatchQueue(IDispatchJobRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask QueueAsync(ReminderDispatchWorkItem workItem, CancellationToken cancellationToken = default)
        {
            var payloadHash = ComputePayloadHash(workItem);

            var existing = await _repository.FindOpenAsync(
                workItem.SessionId,
                workItem.DispatchType,
                payloadHash,
                cancellationToken);
            if (existing is not null)
                return;

            var job = new DispatchJob
            {
                SessionId = workItem.SessionId,
                DispatchType = workItem.DispatchType,
                PayloadJson = JsonSerializer.Serialize(workItem),
                PayloadHash = payloadHash,
                Status = DispatchJobStatus.Pending,
                Attempts = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                await _repository.AddAsync(job, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Carrera con otro productor idéntico: el índice único filtrado ya lo cubrió.
                var confirmed = await _repository.FindOpenAsync(
                    workItem.SessionId,
                    workItem.DispatchType,
                    payloadHash,
                    cancellationToken);
                if (confirmed is not null)
                    return;

                throw;
            }
        }

        /// <summary>
        /// Hash determinístico del contenido del envío, sin metadata de timing/actor
        /// (para que re-encolares posteriores con el mismo contenido no dupliquen).
        /// </summary>
        private static string ComputePayloadHash(ReminderDispatchWorkItem workItem)
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["SessionId"] = workItem.SessionId.ToString(CultureInfo.InvariantCulture),
                ["FechaHora"] = workItem.FechaHora.ToString("o", CultureInfo.InvariantCulture),
                ["PacienteNombre"] = workItem.PacienteNombre,
                ["PacienteEmail"] = workItem.PacienteEmail ?? string.Empty,
                ["PacienteTelefono"] = workItem.PacienteTelefono ?? string.Empty,
                ["ProfesionalNombre"] = workItem.ProfesionalNombre,
                ["TratamientoDescripcion"] = workItem.TratamientoDescripcion ?? string.Empty,
                ["ConfirmUrl"] = workItem.ConfirmUrl,
                ["CancelUrl"] = workItem.CancelUrl,
                ["DispatchType"] = workItem.DispatchType,
                ["AuditEntityName"] = workItem.AuditEntityName,
                ["AuditEntityId"] = workItem.AuditEntityId ?? string.Empty,
                ["EmailSubjectOverride"] = workItem.EmailSubjectOverride ?? string.Empty,
                ["EmailBodyOverride"] = workItem.EmailBodyOverride ?? string.Empty,
                ["WhatsAppBodyOverride"] = workItem.WhatsAppBodyOverride ?? string.Empty
            };

            var canonical = string.Join("|", fields.Select(kv => kv.Key + "=" + kv.Value));
            var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes);
        }
    }

    public sealed class ReminderDispatchBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderDispatchBackgroundService> _logger;
        private readonly int _pollIntervalSeconds;
        private readonly int _batchSize;
        private readonly TimeSpan _leaseDuration;
        private readonly int _maxAttempts;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan _retentionPeriod;

        public ReminderDispatchBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ReminderDispatchBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pollIntervalSeconds = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:PollIntervalSeconds",
                defaultValue: 5,
                min: 1,
                max: 300);
            _batchSize = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:BatchSize",
                defaultValue: 10,
                min: 1,
                max: 100);
            _leaseDuration = TimeSpan.FromSeconds(OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:LeaseSeconds",
                defaultValue: 120,
                min: 30,
                max: 3600));
            _maxAttempts = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:MaxAttempts",
                defaultValue: 5,
                min: 1,
                max: 20);
            _retryDelay = TimeSpan.FromSeconds(OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:RetryBaseDelaySeconds",
                defaultValue: 60,
                min: 5,
                max: 86400));
            _retentionPeriod = TimeSpan.FromDays(OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "DispatchQueue:RetentionDays",
                defaultValue: 30,
                min: 1,
                max: 365));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pollSinceCleanup = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDispatchJobRepository>();
                    var nowUtc = DateTime.UtcNow;

                    var jobs = await repository.ClaimNextBatchAsync(_batchSize, _leaseDuration, nowUtc, stoppingToken);
                    if (jobs.Count == 0)
                    {
                        if (++pollSinceCleanup >= 100)
                        {
                            await repository.CleanupTerminalAsync(nowUtc.Add(-_retentionPeriod), stoppingToken);
                            pollSinceCleanup = 0;
                        }
                    }
                    else
                    {
                        var deliveryService = scope.ServiceProvider.GetRequiredService<IReminderDeliveryService>();
                        var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                        foreach (var job in jobs)
                        {
                            if (stoppingToken.IsCancellationRequested)
                                break;

                            await ProcessJobAsync(job, deliveryService, auditLogService, repository, nowUtc, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el ciclo de despacho de recordatorios en segundo plano.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessJobAsync(
            DispatchJob job,
            IReminderDeliveryService deliveryService,
            IAuditLogService auditLogService,
            IDispatchJobRepository repository,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            ReminderDispatchWorkItem? workItem;
            try
            {
                workItem = JsonSerializer.Deserialize<ReminderDispatchWorkItem>(job.PayloadJson);
            }
            catch (JsonException)
            {
                workItem = null;
            }

            if (workItem is null)
            {
                _logger.LogError("Payload inválido en DispatchJob {JobId} (sesión {SessionId}).", job.Id, job.SessionId);
                await repository.MarkFailedAsync(job.Id, "Payload inválido o vacío.", _retryDelay, _maxAttempts, nowUtc, cancellationToken);
                return;
            }

            try
            {
                var result = await deliveryService.SendAsync(new ReminderDeliveryRequest
                {
                    SessionId = workItem.SessionId,
                    FechaHora = workItem.FechaHora,
                    PacienteNombre = workItem.PacienteNombre,
                    PacienteEmail = workItem.PacienteEmail,
                    PacienteTelefono = workItem.PacienteTelefono,
                    ProfesionalNombre = workItem.ProfesionalNombre,
                    TratamientoDescripcion = workItem.TratamientoDescripcion,
                    ConfirmUrl = workItem.ConfirmUrl,
                    CancelUrl = workItem.CancelUrl,
                    EmailSubjectOverride = workItem.EmailSubjectOverride,
                    EmailBodyOverride = workItem.EmailBodyOverride,
                    WhatsAppBodyOverride = workItem.WhatsAppBodyOverride
                }, cancellationToken);

                await auditLogService.AddAsync(new KineGestion.Core.Entities.AuditLog
                {
                    EntityName = string.IsNullOrWhiteSpace(workItem.AuditEntityName) ? "ReminderDispatch" : workItem.AuditEntityName,
                    EntityId = string.IsNullOrWhiteSpace(workItem.AuditEntityId)
                        ? workItem.SessionId.ToString(CultureInfo.InvariantCulture)
                        : workItem.AuditEntityId,
                    Action = "Create",
                    ChangedBy = string.IsNullOrWhiteSpace(workItem.ChangedBy) ? "system" : workItem.ChangedBy,
                    ChangedAt = nowUtc,
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        workItem.DispatchType,
                        workItem.SessionId,
                        workItem.FechaHora,
                        workItem.PacienteNombre,
                        workItem.PacienteEmail,
                        workItem.PacienteTelefono,
                        EmailSent = result.EmailSent,
                        WhatsAppSent = result.WhatsAppSent,
                        Errors = result.Errors,
                        workItem.EnqueuedAtUtc
                    })
                });

                await repository.MarkSucceededAsync(job.Id, JsonSerializer.Serialize(result), nowUtc, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando recordatorio en segundo plano para sesión {SessionId}.", workItem.SessionId);
                await repository.MarkFailedAsync(job.Id, ex.Message, _retryDelay, _maxAttempts, nowUtc, cancellationToken);
            }
        }
    }
}