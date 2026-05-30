using System.Globalization;
using System.Text.Json;
using KineGestion.Core.Interfaces;
using System.Threading.Channels;

namespace KineGestion.Web.Services
{
    public interface IReminderDispatchQueue
    {
        ValueTask QueueAsync(ReminderDispatchWorkItem workItem, CancellationToken cancellationToken = default);
        ValueTask<ReminderDispatchWorkItem> DequeueAsync(CancellationToken cancellationToken);
    }

    public sealed class ReminderDispatchQueue : IReminderDispatchQueue
    {
        private readonly Channel<ReminderDispatchWorkItem> _queue;

        public ReminderDispatchQueue()
        {
            _queue = Channel.CreateBounded<ReminderDispatchWorkItem>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public ValueTask QueueAsync(ReminderDispatchWorkItem workItem, CancellationToken cancellationToken = default)
            => _queue.Writer.WriteAsync(workItem, cancellationToken);

        public ValueTask<ReminderDispatchWorkItem> DequeueAsync(CancellationToken cancellationToken)
            => _queue.Reader.ReadAsync(cancellationToken);
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
    }

    public sealed class ReminderDispatchBackgroundService : BackgroundService
    {
        private readonly IReminderDispatchQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderDispatchBackgroundService> _logger;

        public ReminderDispatchBackgroundService(
            IReminderDispatchQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<ReminderDispatchBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ReminderDispatchWorkItem workItem;

                try
                {
                    workItem = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var deliveryService = scope.ServiceProvider.GetRequiredService<IReminderDeliveryService>();
                    var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

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
                        CancelUrl = workItem.CancelUrl
                    }, stoppingToken);

                    await auditLogService.AddAsync(new KineGestion.Core.Entities.AuditLog
                    {
                        EntityName = "ReminderDispatch",
                        EntityId = workItem.SessionId.ToString(CultureInfo.InvariantCulture),
                        Action = "Create",
                        ChangedBy = string.IsNullOrWhiteSpace(workItem.ChangedBy) ? "system" : workItem.ChangedBy,
                        ChangedAt = DateTime.UtcNow,
                        NewValuesJson = JsonSerializer.Serialize(new
                        {
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando recordatorio en segundo plano para sesión {SessionId}", workItem.SessionId);
                }
            }
        }
    }
}