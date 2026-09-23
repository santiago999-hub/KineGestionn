using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KineGestion.Web.Tests
{
    public class ReminderFunnelControllerTests
    {
        [Fact]
        public async Task Index_ShouldReturnFunnel_ForSentPatientRemindersInRange()
        {
            var dispatchRepository = new Mock<IDispatchEventRepository>();
            var sessionService = new Mock<ISessionService>();

            var from = new DateTime(2026, 8, 1);
            var toExclusive = new DateTime(2026, 9, 1);

            var dispatches = new List<DispatchEvent>
            {
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = 1,
                    EmailSent = true,
                    WhatsAppSent = false
                },
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = 2,
                    EmailSent = false,
                    WhatsAppSent = false
                }
            };

            dispatchRepository
                .Setup(r => r.GetByTypeAsync("PatientReminder", from, new DateTime(2026, 8, 31), null))
                .ReturnsAsync(dispatches);

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1), from, toExclusive))
                .ReturnsAsync(new ReminderFunnelDto(1, 1, 1) { Canceled = 0 });

            var controller = new ReminderFunnelController(sessionService.Object, dispatchRepository.Object);

            var result = await controller.Index(from, new DateTime(2026, 8, 31));

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReminderFunnelDashboardViewModel>(viewResult.Model);
            Assert.Equal(1, model.Funnel.Sent);
            Assert.Equal(1, model.Funnel.Confirmed);
            Assert.Equal(1, model.Funnel.Attended);

            // Solo la sesión 1 se contó (envío real); la 2 no se envió.
            sessionService.Verify(s => s.BuildReminderFunnelAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(1)),
                from,
                toExclusive), Times.Once);
            dispatchRepository.Verify(r => r.GetByTypeAsync("PatientReminder", from, new DateTime(2026, 8, 31), null), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldIgnoreSessionsWithoutId_AndUnsentEvents()
        {
            var dispatchRepository = new Mock<IDispatchEventRepository>();
            var sessionService = new Mock<ISessionService>();

            var from = new DateTime(2026, 8, 1);
            var toExclusive = new DateTime(2026, 9, 1);

            var dispatches = new List<DispatchEvent>
            {
                // Sin SessionId: se descarta aunque el evento indique envío.
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = null,
                    EmailSent = true,
                    WhatsAppSent = false
                },
                // No enviado por ningún canal: se descarta.
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = 4,
                    EmailSent = false,
                    WhatsAppSent = false
                },
                // Enviado por email: se cuenta.
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = 5,
                    EmailSent = true,
                    WhatsAppSent = false
                },
                // Enviado por WhatsApp: se cuenta.
                new DispatchEvent
                {
                    DispatchType = "PatientReminder",
                    SessionId = 6,
                    EmailSent = false,
                    WhatsAppSent = true
                }
            };

            dispatchRepository
                .Setup(r => r.GetByTypeAsync("PatientReminder", from, new DateTime(2026, 8, 31), null))
                .ReturnsAsync(dispatches);

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.IsAny<IReadOnlyCollection<int>>(), from, toExclusive))
                .ReturnsAsync(new ReminderFunnelDto(0, 0, 0));

            var controller = new ReminderFunnelController(sessionService.Object, dispatchRepository.Object);

            var result = await controller.Index(from, new DateTime(2026, 8, 31));

            Assert.IsType<ViewResult>(result);

            sessionService.Verify(s => s.BuildReminderFunnelAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(5) && ids.Contains(6)),
                from,
                toExclusive), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldDefaultToLast30Days_WhenNoDatesProvided()
        {
            var dispatchRepository = new Mock<IDispatchEventRepository>();
            var sessionService = new Mock<ISessionService>();

            // Sin despachos, el funnel queda vacío (ceros).
            dispatchRepository
                .Setup(r => r.GetByTypeAsync("PatientReminder", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), null))
                .ReturnsAsync(Array.Empty<DispatchEvent>());

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ReminderFunnelDto(0, 0, 0));

            var controller = new ReminderFunnelController(sessionService.Object, dispatchRepository.Object);

            var result = await controller.Index(null, null);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReminderFunnelDashboardViewModel>(viewResult.Model);

            var today = DateTime.UtcNow.Date;
            Assert.True(model.DateFrom >= today.AddDays(-31) && model.DateFrom <= today.AddDays(-29));
            Assert.Equal(0, model.Funnel.Sent);
            Assert.Equal(0, model.Funnel.Confirmed);
            Assert.Equal(0, model.Funnel.Attended);

            sessionService.Verify(s => s.BuildReminderFunnelAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 0),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()), Times.Once);
        }
    }
}