using KineGestion.Core.DTOs;
using KineGestion.Core.Interfaces;

namespace KineGestion.Web.Services
{
    public interface IBillingFollowUpAutomationService
    {
        Task<BillingFollowUpAutomationRunResult> RunAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    }

    public sealed class BillingFollowUpAutomationRunResult
    {
        public int TotalCandidates { get; set; }
        public int Queued { get; set; }
        public int SkippedAlreadyNotified { get; set; }
        public int SkippedNoTier { get; set; }
        public int SkippedOutOfRange { get; set; }

        public override string ToString()
            => $"Candidatos={TotalCandidates}, Encolados={Queued}, YaNotificados={SkippedAlreadyNotified}, SinNivel={SkippedNoTier}, FueraDeRango={SkippedOutOfRange}";
    }

    /// <summary>
    /// Automatización D+1 de cobranza: cada corrida recorre el aging de sesiones
    /// completadas sin pago y encola el primer nivel de cobro no enviado
    /// (D+1 suave &gt; D+3 recordatorio &gt; D+7 firme). Nunca reenvía un nivel superior
    /// ya despachado, por lo que el mensaje no se vuelve spam.
    /// </summary>
    public sealed class BillingFollowUpAutomationService : IBillingFollowUpAutomationService
    {
        private const string AutomationActor = "system:automation";

        private readonly ISessionService _sessionService;
        private readonly IBillingFollowUpService _billingFollowUpService;
        private readonly IReminderDispatchQueue _reminderDispatchQueue;
        private readonly IDispatchJobRepository _dispatchJobRepository;
        private readonly IConfiguration _configuration;

        public BillingFollowUpAutomationService(
            ISessionService sessionService,
            IBillingFollowUpService billingFollowUpService,
            IReminderDispatchQueue reminderDispatchQueue,
            IDispatchJobRepository dispatchJobRepository,
            IConfiguration configuration)
        {
            _sessionService = sessionService;
            _billingFollowUpService = billingFollowUpService;
            _reminderDispatchQueue = reminderDispatchQueue;
            _dispatchJobRepository = dispatchJobRepository;
            _configuration = configuration;
        }

        public async Task<BillingFollowUpAutomationRunResult> RunAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var result = new BillingFollowUpAutomationRunResult();
            var minAge = MinAgeDays();
            var maxAge = MaxAgeDays();

            var candidates = (await _sessionService.GetBillingFollowUpCandidatesAsync(asOfUtc, minAge, maxAge)).ToList();
            var campaign = _billingFollowUpService.BuildCampaign(candidates);

            var sessionIds = campaign.Candidates.Keys.ToList();
            var dispatched = await _dispatchJobRepository.GetDispatchedBillingTypesAsync(sessionIds, cancellationToken);

            result.TotalCandidates = campaign.Candidates.Count;

            foreach (var candidate in campaign.Candidates.Values.OrderBy(c => c.FechaHora))
            {
                if (candidate.PaymentAgeDays < minAge || candidate.PaymentAgeDays > maxAge)
                {
                    result.SkippedOutOfRange++;
                    continue;
                }

                var tier = campaign.GetTierForCandidate(candidate);
                if (tier is null)
                {
                    result.SkippedNoTier++;
                    continue;
                }

                var lastTier = dispatched.TryGetValue(candidate.SessionId, out var dispatchTypes)
                    ? MaxDispatchedTier(dispatchTypes)
                    : null;

                if (lastTier is not null && TierRank(tier.Tier) <= TierRank(lastTier.Value))
                {
                    result.SkippedAlreadyNotified++;
                    continue;
                }

                await _reminderDispatchQueue.QueueAsync(BuildWorkItem(candidate, tier), cancellationToken);
                result.Queued++;
            }

            return result;
        }

        private ReminderDispatchWorkItem BuildWorkItem(BillingFollowUpCandidateDto candidate, BillingFollowUpTierDefinition tier)
            => new ReminderDispatchWorkItem
            {
                SessionId = candidate.SessionId,
                FechaHora = candidate.FechaHora,
                PacienteNombre = candidate.PacienteNombre,
                PacienteEmail = candidate.PacienteEmail,
                PacienteTelefono = candidate.PacienteTelefono,
                ProfesionalNombre = candidate.ProfesionalNombre,
                TratamientoDescripcion = candidate.TratamientoDescripcion,
                ChangedBy = AutomationActor,
                EnqueuedAtUtc = DateTime.UtcNow,
                DispatchType = "BillingFollowUp:" + tier.Tier,
                EmailSubjectOverride = tier.EmailSubject,
                EmailBodyOverride = tier.EmailBody,
                WhatsAppBodyOverride = tier.WhatsAppBody,
                AuditEntityName = "BillingFollowUp"
            };

        private static BillingFollowUpTier? MaxDispatchedTier(IReadOnlyList<string> dispatchTypes)
        {
            BillingFollowUpTier? max = null;
            foreach (var dispatchType in dispatchTypes)
            {
                var tier = TryParseTier(dispatchType);
                if (tier is null)
                    continue;

                if (max is null || TierRank(tier.Value) > TierRank(max.Value))
                    max = tier;
            }

            return max;
        }

        private static BillingFollowUpTier? TryParseTier(string dispatchType)
        {
            var prefix = "BillingFollowUp:";
            if (!dispatchType.StartsWith(prefix, StringComparison.Ordinal))
                return null;

            return Enum.TryParse<BillingFollowUpTier>(dispatchType[prefix.Length..], ignoreCase: true, out var tier)
                ? tier
                : null;
        }

        private static int TierRank(BillingFollowUpTier tier) => tier switch
        {
            BillingFollowUpTier.Soft => 0,
            BillingFollowUpTier.Reminder => 1,
            BillingFollowUpTier.Firm => 2,
            _ => int.MaxValue
        };

        private int MinAgeDays()
            => Math.Max(0, _configuration.GetValue<int?>("BillingFollowUp:MinAgeDays") ?? 1);

        private int MaxAgeDays()
            => Math.Max(MinAgeDays(), _configuration.GetValue<int?>("BillingFollowUp:MaxAgeDays") ?? 9999);
    }
}