using System;
using System.Collections.Generic;
using System.Linq;
using KineGestion.Core.DTOs;
using Microsoft.Extensions.Configuration;

namespace KineGestion.Web.Services
{
    public enum BillingFollowUpTier
    {
        Soft,
        Reminder,
        Firm
    }

    public sealed class BillingFollowUpTierDefinition
    {
        public BillingFollowUpTier Tier { get; init; }
        public int MinAgeDays { get; init; }
        public int MaxAgeDays { get; init; }
        public string EmailSubject { get; init; } = string.Empty;
        public string EmailBody { get; init; } = string.Empty;
        public string WhatsAppBody { get; init; } = string.Empty;

        public bool Matches(int ageDays) => ageDays >= MinAgeDays && ageDays <= MaxAgeDays;

        public string Label => Tier switch
        {
            BillingFollowUpTier.Soft => "D+1 - Suave",
            BillingFollowUpTier.Reminder => "D+3 - Recordatorio",
            BillingFollowUpTier.Firm => "D+7 - Firme",
            _ => "Otro"
        };

        public string BadgeClass => Tier switch
        {
            BillingFollowUpTier.Soft => "text-bg-info",
            BillingFollowUpTier.Reminder => "text-bg-warning",
            BillingFollowUpTier.Firm => "text-bg-danger",
            _ => "text-bg-secondary"
        };
    }

    public sealed class BillingFollowUpCampaign
    {
        public List<BillingFollowUpTierDefinition> Tiers { get; set; } = new();
        public Dictionary<int, BillingFollowUpCandidateDto> Candidates { get; set; } = new();
        public BillingFollowUpTierDefinition? GetTierForCandidate(BillingFollowUpCandidateDto candidate)
            => Tiers.FirstOrDefault(t => t.Matches(candidate.PaymentAgeDays));
    }

    public interface IBillingFollowUpService
    {
        BillingFollowUpCampaign BuildCampaign(IReadOnlyCollection<BillingFollowUpCandidateDto> candidates);
        BillingFollowUpTierDefinition ConfigureTier(BillingFollowUpTier tier);
    }

    public sealed class BillingFollowUpService : IBillingFollowUpService
    {
        private readonly IConfiguration _configuration;

        public BillingFollowUpService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public BillingFollowUpCampaign BuildCampaign(IReadOnlyCollection<BillingFollowUpCandidateDto> candidates)
        {
            var campaign = new BillingFollowUpCampaign
            {
                Tiers = new List<BillingFollowUpTierDefinition>
                {
                    ConfigureTier(BillingFollowUpTier.Soft),
                    ConfigureTier(BillingFollowUpTier.Reminder),
                    ConfigureTier(BillingFollowUpTier.Firm)
                }
            };

            foreach (var candidate in candidates)
            {
                campaign.Candidates[candidate.SessionId] = candidate;
            }

            return campaign;
        }

        public BillingFollowUpTierDefinition ConfigureTier(BillingFollowUpTier tier)
        {
            var prefix = "BillingFollowUp:Tiers:" + tier.ToString();

            var minAge = ReadAgeDay(_configuration[prefix + ":MinAgeDays"], tier switch
            {
                BillingFollowUpTier.Soft => 1,
                BillingFollowUpTier.Reminder => 3,
                BillingFollowUpTier.Firm => 7,
                _ => 1
            });

            var maxAge = ReadAgeDay(_configuration[prefix + ":MaxAgeDays"], tier switch
            {
                BillingFollowUpTier.Soft => 2,
                BillingFollowUpTier.Reminder => 6,
                BillingFollowUpTier.Firm => 9999,
                _ => 9999
            });

            if (maxAge < minAge)
                maxAge = minAge;

            return new BillingFollowUpTierDefinition
            {
                Tier = tier,
                MinAgeDays = minAge,
                MaxAgeDays = maxAge,
                EmailSubject = _configuration[prefix + ":EmailSubject"] ?? DefaultEmailSubject(tier),
                EmailBody = _configuration[prefix + ":EmailBody"] ?? DefaultEmailBody(tier),
                WhatsAppBody = _configuration[prefix + ":WhatsAppBody"] ?? DefaultWhatsAppBody(tier)
            };
        }

        private static int ReadAgeDay(string? raw, int defaultValue)
            => int.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : defaultValue;

        private static string DefaultEmailSubject(BillingFollowUpTier tier) => tier switch
        {
            BillingFollowUpTier.Soft => "{{ClinicName}} - Tu sesión está lista para abonar",
            BillingFollowUpTier.Reminder => "{{ClinicName}} - Recordatorio de pago pendiente",
            BillingFollowUpTier.Firm => "{{ClinicName}} - Regularización urgente de tu saldo",
            _ => "{{ClinicName}} - Cobranza"
        };

        private static string DefaultEmailBody(BillingFollowUpTier tier) => tier switch
        {
            BillingFollowUpTier.Soft =>
                "Hola {{PatientName}}, esperamos que tu sesión haya sido de gran ayuda.\\n" +
                "Te recordamos que podés abonar tu sesión de {{SessionDateTime}}.\\n" +
                "Para más info: {{ContactPhone}} | {{ContactEmail}}\\n\\n{{Signature}}",
            BillingFollowUpTier.Reminder =>
                "Hola {{PatientName}}, este es un recordatorio de {{ClinicName}}.\\n" +
                "Todavía tenés pendiente de pago la sesión del {{SessionDateTime}}.\\n" +
                "Te pedimos regularizar el saldo a la brevedad.\\n" +
                "Contacto: {{ContactPhone}} | {{ContactEmail}}\\n\\n{{Signature}}",
            BillingFollowUpTier.Firm =>
                "Hola {{PatientName}}, nos comunicamos de {{ClinicName}}.\\n" +
                "Tu sesión del {{SessionDateTime}} sigue pendiente de pago.\\n" +
                "Necesitamos regularizar este saldo. Comunicate al {{ContactPhone}} o {{ContactEmail}} para resolverlo.\\n\\n{{Signature}}",
            _ => "Hola {{PatientName}}, pendiente de pago de {{ClinicName}}. {{ContactPhone}} | {{ContactEmail}}"
        };

        private static string DefaultWhatsAppBody(BillingFollowUpTier tier) => tier switch
        {
            BillingFollowUpTier.Soft =>
                "Hola {{PatientName}}! Recordamos que puedes abonar tu sesión de {{SessionDateTime}}. Gracias por elegir {{ClinicName}}.",
            BillingFollowUpTier.Reminder =>
                "Hola {{PatientName}}, recordatorio de {{ClinicName}}: tenés pendiente de pago la sesión del {{SessionDateTime}}. Te pedimos regularizar a la brevedad.",
            BillingFollowUpTier.Firm =>
                "Hola {{PatientName}}, de {{ClinicName}}. Tu sesión del {{SessionDateTime}} sigue sin abonar. Necesitamos regularizar este saldo, comunicate con nosotros.",
            _ => "Hola {{PatientName}}, pendiente de pago de {{ClinicName}}."
        };
    }
}
