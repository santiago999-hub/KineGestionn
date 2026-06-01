using System.Globalization;
using System.Text;
using System.Text.Json;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace KineGestion.Web.Services
{
    public interface IBillingOperationalAlertService
    {
        Task<BillingOperationalAlertSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken cancellationToken = default);
        Task<BillingOperationalAlertDispatchResult> QueueAlertIfNeededAsync(string? changedBy, DateTime nowUtc, CancellationToken cancellationToken = default);
    }

    public sealed class BillingOperationalAlertSnapshot
    {
        public decimal ThresholdPct { get; set; }
        public bool HasConsecutiveLowWeeks { get; set; }
        public List<ReminderBillingTrendPointViewModel> TrendPoints { get; set; } = new();
    }

    public sealed class BillingOperationalAlertDispatchResult
    {
        public bool Queued { get; set; }
        public bool AlreadySentToday { get; set; }
        public bool NoDestinationConfigured { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class BillingOperationalAlertService : IBillingOperationalAlertService
    {
        private const string SnapshotCacheKeyPrefix = "BillingOperationalAlertService.Snapshot";

        private readonly IAuditLogService _auditLogService;
        private readonly IReminderDispatchQueue _reminderDispatchQueue;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public BillingOperationalAlertService(
            IAuditLogService auditLogService,
            IReminderDispatchQueue reminderDispatchQueue,
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _auditLogService = auditLogService;
            _reminderDispatchQueue = reminderDispatchQueue;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public async Task<BillingOperationalAlertSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken cancellationToken = default)
        {
            var thresholdPct = _configuration.GetValue<decimal?>("Billing:BatchEffectivenessWarnThresholdPct") ?? 70m;
            var cacheKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{SnapshotCacheKeyPrefix}:{referenceUtc:yyyyMMdd}:{thresholdPct:F2}");

            if (_memoryCache.TryGetValue(cacheKey, out BillingOperationalAlertSnapshot? cachedSnapshot) && cachedSnapshot is not null)
            {
                return cachedSnapshot;
            }

            var windowStart = referenceUtc.Date.AddDays(-27);
            var windowEnd = referenceUtc.Date;

            var billingBatchLogs = await _auditLogService.GetAllAsync(
                entityName: "BillingBatch",
                entityId: null,
                changedBy: null,
                action: "Create",
                dateFrom: windowStart,
                dateTo: windowEnd);

            var trendPoints = BuildBillingWeeklyTrend(billingBatchLogs, windowEnd, weeks: 4);

            var snapshot = new BillingOperationalAlertSnapshot
            {
                ThresholdPct = thresholdPct,
                HasConsecutiveLowWeeks = HasConsecutiveLowWeeks(trendPoints, thresholdPct, requiredConsecutiveWeeks: 2),
                TrendPoints = trendPoints
            };

            _memoryCache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(15)
            });

            return snapshot;
        }

        public async Task<BillingOperationalAlertDispatchResult> QueueAlertIfNeededAsync(string? changedBy, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            if (!(_configuration.GetValue<bool?>("Reminders:OperationalAlerts:Enabled") ?? true))
            {
                return new BillingOperationalAlertDispatchResult { Message = string.Empty };
            }

            var snapshot = await GetSnapshotAsync(nowUtc, cancellationToken);
            if (!snapshot.HasConsecutiveLowWeeks)
            {
                return new BillingOperationalAlertDispatchResult { Message = string.Empty };
            }

            var dailyAlertId = $"BillingBatchLowEffectiveness:{nowUtc:yyyyMMdd}";
            var todayStart = nowUtc.Date;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            var existingAlerts = await _auditLogService.GetAllAsync(
                entityName: "OperationalAlert",
                entityId: dailyAlertId,
                changedBy: null,
                action: "Create",
                dateFrom: todayStart,
                dateTo: todayEnd);

            if (existingAlerts.Any())
            {
                return new BillingOperationalAlertDispatchResult
                {
                    AlreadySentToday = true,
                    Message = "Alerta operativa de cobranzas ya enviada hoy."
                };
            }

            var adminEmail = _configuration["Reminders:OperationalAlerts:AdminEmail"]
                ?? _configuration["Reminders:Brand:ContactEmail"]
                ?? _configuration["Seed:AdminEmail"];
            var adminPhone = _configuration["Reminders:OperationalAlerts:AdminPhone"]
                ?? _configuration["Reminders:Brand:ContactPhone"];

            if (string.IsNullOrWhiteSpace(adminEmail) && string.IsNullOrWhiteSpace(adminPhone))
            {
                return new BillingOperationalAlertDispatchResult
                {
                    NoDestinationConfigured = true,
                    Message = "Alerta detectada sin destino administrativo configurado."
                };
            }

            await _reminderDispatchQueue.QueueAsync(new ReminderDispatchWorkItem
            {
                SessionId = 0,
                FechaHora = nowUtc,
                PacienteNombre = "Administrador",
                PacienteEmail = adminEmail,
                PacienteTelefono = adminPhone,
                ProfesionalNombre = "Operaciones",
                TratamientoDescripcion = "Alerta de cobranza",
                ChangedBy = changedBy,
                EnqueuedAtUtc = nowUtc,
                DispatchType = "BillingBatchLowEffectivenessAlert",
                AuditEntityName = "OperationalAlert",
                AuditEntityId = dailyAlertId,
                EmailSubjectOverride = BuildSubject(),
                EmailBodyOverride = BuildEmailBody(snapshot),
                WhatsAppBodyOverride = BuildWhatsAppBody(snapshot)
            }, cancellationToken);

            return new BillingOperationalAlertDispatchResult
            {
                Queued = true,
                Message = "Alerta operativa de cobranzas encolada para administración."
            };
        }

        private static List<ReminderBillingTrendPointViewModel> BuildBillingWeeklyTrend(IEnumerable<AuditLog> logs, DateTime endDateInclusiveUtc, int weeks)
        {
            var points = new List<ReminderBillingTrendPointViewModel>();

            for (var index = weeks - 1; index >= 0; index--)
            {
                var weekStart = endDateInclusiveUtc.Date.AddDays(-(index * 7) - 6);
                var weekEnd = weekStart.AddDays(6);

                var point = new ReminderBillingTrendPointViewModel
                {
                    Label = $"{weekStart:dd/MM}-{weekEnd:dd/MM}"
                };

                foreach (var log in logs.Where(l => l.ChangedAt.Date >= weekStart && l.ChangedAt.Date <= weekEnd))
                {
                    if (!TryReadBatchCounters(log.NewValuesJson, out var requested, out var updated, out var skipped))
                        continue;

                    point.RequestedCount += requested;
                    point.UpdatedCount += updated;
                    point.SkippedCount += skipped;
                }

                points.Add(point);
            }

            return points;
        }

        private static bool HasConsecutiveLowWeeks(
            IReadOnlyCollection<ReminderBillingTrendPointViewModel> points,
            decimal thresholdPct,
            int requiredConsecutiveWeeks)
        {
            if (points.Count == 0 || requiredConsecutiveWeeks <= 1)
                return false;

            var consecutive = 0;
            foreach (var point in points)
            {
                var isLow = point.RequestedCount > 0 && point.EffectivenessPct < thresholdPct;
                consecutive = isLow ? consecutive + 1 : 0;

                if (consecutive >= requiredConsecutiveWeeks)
                    return true;
            }

            return false;
        }

        private static bool TryReadBatchCounters(string? json, out int requested, out int updated, out int skipped)
        {
            requested = 0;
            updated = 0;
            skipped = 0;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                requested = ReadInt(root, "RequestedCount");
                updated = ReadInt(root, "UpdatedCount");
                skipped = ReadInt(root, "SkippedCount");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadInt(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var property))
                return 0;

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt32(out var value) => value,
                JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
                _ => 0
            };
        }

        private static string BuildSubject()
            => "Alerta operativa de cobranzas - 2 semanas bajo umbral";

        private static string BuildEmailBody(BillingOperationalAlertSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Se detectaron 2 semanas consecutivas con baja efectividad en lotes de cobranzas.");
            builder.AppendLine($"Umbral configurado: {snapshot.ThresholdPct:N2}%");
            builder.AppendLine();
            builder.AppendLine("Resumen 4 semanas:");

            foreach (var point in snapshot.TrendPoints)
                builder.AppendLine($"- {point.Label}: {point.EffectivenessPct:N2}% | Req {point.RequestedCount} | Upd {point.UpdatedCount} | Skip {point.SkippedCount}");

            return builder.ToString();
        }

        private static string BuildWhatsAppBody(BillingOperationalAlertSnapshot snapshot)
        {
            var latestPoints = snapshot.TrendPoints.TakeLast(2).ToList();
            var latestSummary = string.Join(" | ", latestPoints.Select(p => $"{p.Label}: {p.EffectivenessPct:N2}%"));
            return $"Alerta cobranzas: 2 semanas consecutivas bajo umbral ({snapshot.ThresholdPct:N2}%). {latestSummary}";
        }
    }
}