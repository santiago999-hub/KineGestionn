using KineGestion.Core.DTOs;
using Xunit;

namespace KineGestion.Tests
{
    public class KpiSegmentDtoTests
    {
        [Fact]
        public void CumplimientoPct_ShouldUseScheduledAsDenominator()
        {
            var dto = new KpiSegmentDto("1", "Diaz, Jose", Total: 10, Pending: 2, Completed: 8, Canceled: 0, CompletedPending: 8, CompletedPaid: 0);
            Assert.Equal(80m, dto.CumplimientoPct);
        }

        [Fact]
        public void CobranzaPct_ShouldUseCompletedAsDenominator()
        {
            var dto = new KpiSegmentDto("1", "Diaz, Jose", Total: 10, Pending: 0, Completed: 10, Canceled: 0, CompletedPending: 2, CompletedPaid: 8);
            Assert.Equal(80m, dto.CobranzaPct);
        }

        [Fact]
        public void CancelacionPct_ShouldUseAgendadasAsDenominator()
        {
            var dto = new KpiSegmentDto("1", "Diaz, Jose", Total: 20, Pending: 10, Completed: 5, Canceled: 5, CompletedPending: 5, CompletedPaid: 0);
            Assert.Equal(25m, dto.CancelacionPct);
        }

        [Fact]
        public void Pcts_ShouldReturnZero_WhenNoData()
        {
            var dto = new KpiSegmentDto("1", "Diaz, Jose", Total: 0, Pending: 0, Completed: 0, Canceled: 0, CompletedPending: 0, CompletedPaid: 0);
            Assert.Equal(0m, dto.CumplimientoPct);
            Assert.Equal(0m, dto.CobranzaPct);
            Assert.Equal(0m, dto.CancelacionPct);
        }
    }
}
