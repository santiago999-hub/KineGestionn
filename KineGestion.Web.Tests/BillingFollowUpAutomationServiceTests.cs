using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace KineGestion.Web.Tests
{
    public class BillingFollowUpAutomationServiceTests
    {
        [Fact]
        public async Task RunAsync_ShouldEnqueueFirstTier_AndEscalateWithoutResending()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[]
                {
                    Candidate(10, days: 1),  // Soft, sin envíos previos → encolar
                    Candidate(11, days: 4),  // Reminder, ya enviado Soft → escalar
                    Candidate(12, days: 9),  // Firm, ya enviado Firm → saltar
                    Candidate(13, days: 9)   // Firm, ya enviado Reminder → escalar
                });

            var dispatchRepository = new Mock<IDispatchJobRepository>();
            dispatchRepository
                .Setup(r => r.GetDispatchedBillingTypesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<string>>
                {
                    [11] = new[] { "BillingFollowUp:Soft" },
                    [12] = new[] { "BillingFollowUp:Firm" },
                    [13] = new[] { "BillingFollowUp:Reminder" }
                });

            var queued = new List<ReminderDispatchWorkItem>();
            var queue = new Mock<IReminderDispatchQueue>();
            queue
                .Setup(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), It.IsAny<CancellationToken>()))
                .Callback<ReminderDispatchWorkItem, CancellationToken>((item, _) => queued.Add(item))
                .Returns(new ValueTask());

            var service = new BillingFollowUpAutomationService(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                queue.Object,
                dispatchRepository.Object,
                BuildEmptyConfiguration());

            var result = await service.RunAsync(new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc));

            Assert.Equal(4, result.TotalCandidates);
            Assert.Equal(3, result.Queued);
            Assert.Equal(1, result.SkippedAlreadyNotified);
            Assert.Equal(0, result.SkippedNoTier);

            Assert.Equal(new[] { 10, 11, 13 }, queued.Select(q => q.SessionId).OrderBy(x => x).ToArray());
            Assert.Equal("BillingFollowUp:Soft", queued.Single(q => q.SessionId == 10).DispatchType);
            Assert.Equal("BillingFollowUp:Reminder", queued.Single(q => q.SessionId == 11).DispatchType);
            Assert.Equal("BillingFollowUp:Firm", queued.Single(q => q.SessionId == 13).DispatchType);
            Assert.All(queued, q => Assert.Equal("system:automation", q.ChangedBy));
            Assert.All(queued, q => Assert.Equal("BillingFollowUp", q.AuditEntityName));
        }

        [Fact]
        public async Task RunAsync_WithoutCandidates_ShouldEnqueueNothing()
        {
            var sessionService = new Mock<ISessionService>();
            sessionService
                .Setup(s => s.GetBillingFollowUpCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<BillingFollowUpCandidateDto>());

            var queue = new Mock<IReminderDispatchQueue>();
            var dispatchRepository = new Mock<IDispatchJobRepository>();
            dispatchRepository
                .Setup(r => r.GetDispatchedBillingTypesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<string>>());

            var service = new BillingFollowUpAutomationService(
                sessionService.Object,
                new BillingFollowUpService(BuildEmptyConfiguration()),
                queue.Object,
                dispatchRepository.Object,
                BuildEmptyConfiguration());

            var result = await service.RunAsync(new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc));

            Assert.Equal(0, result.TotalCandidates);
            Assert.Equal(0, result.Queued);
            queue.Verify(q => q.QueueAsync(It.IsAny<ReminderDispatchWorkItem>(), default), Times.Never);
        }

        private static IConfiguration BuildEmptyConfiguration()
            => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        private static BillingFollowUpCandidateDto Candidate(int sessionId, int days)
            => new BillingFollowUpCandidateDto(
                sessionId,
                new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                "Perez, Juan",
                "juan@test.com",
                "5491122334455",
                "Gomez, Ana",
                "Rehabilitacion",
                days);
    }
}