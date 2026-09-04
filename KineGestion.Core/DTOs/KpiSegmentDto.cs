using System;

namespace KineGestion.Core.DTOs
{
    /// <summary>
    /// Agregación de KPIs por segmento (profesional o franja horaria) para identificar
    /// dónde se fuga cumplimiento o cobranza. Calculada en una única consulta SQL de
    /// agregación, sin cargar entidades completas.
    /// </summary>
    public record KpiSegmentDto(
        string SegmentKey,
        string SegmentLabel,
        int Total,
        int Pending,
        int Completed,
        int Canceled,
        int CompletedPending,
        int CompletedPaid)
    {
        /// <summary>Tasa de cumplimiento = completadas / agendadas (Pending + Completed).</summary>
        public decimal CumplimientoPct
            => (Pending + Completed) > 0
                ? Math.Round((decimal)Completed * 100m / (Pending + Completed), 2)
                : 0m;

        /// <summary>Tasa de cobranza = pagadas / completadas (mismo segmento).</summary>
        public decimal CobranzaPct
            => Completed > 0
                ? Math.Round((decimal)CompletedPaid * 100m / Completed, 2)
                : 0m;

        /// <summary>Tasa de cancelación = canceladas / agendadas.</summary>
        public decimal CancelacionPct
            => (Pending + Completed + Canceled) > 0
                ? Math.Round((decimal)Canceled * 100m / (Pending + Completed + Canceled), 2)
                : 0m;
    }
}
