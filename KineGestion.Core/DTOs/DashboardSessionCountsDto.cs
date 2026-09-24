namespace KineGestion.Core.DTOs
{
    public record DashboardSessionCountsDto(
        int Total,
        int TodayTotal,
        int TodayCompleted,
        int TodayCanceled,
        int CompletedPendingAllTime,
        int PendingAllTime,
        int CompletedInRange,
        int PaidCompletedInRange,
        int TotalInRange,
        int CanceledInRange,
        int LateCancellationsInRange);
}