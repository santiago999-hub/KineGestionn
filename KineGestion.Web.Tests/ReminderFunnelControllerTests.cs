using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            var auditLogService = new Mock<IAuditLogService>();
            var sessionService = new Mock<ISessionService>();

            var from = new DateTime(2026, 8, 1);
            var toExclusive = new DateTime(2026, 9, 1);

            var dispatches = new List<AuditLog>
            {
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "1",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        DispatchType = "PatientReminder",
                        SessionId = 1,
                        EmailSent = true,
                        WhatsAppSent = false
                    })
                },
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "2",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        DispatchType = "PatientReminder",
                        SessionId = 2,
                        EmailSent = false,
                        WhatsAppSent = false
                    })
                },
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "3",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        DispatchType = "BillingFollowUp:T1",
                        SessionId = 3,
                        EmailSent = true,
                        WhatsAppSent = false
                    })
                }
            };

            auditLogService
                .Setup(a => a.GetAllAsync("ReminderDispatch", null, null, "Create", from, new DateTime(2026, 8, 31)))
                .ReturnsAsync(dispatches);

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1), from, toExclusive))
                .ReturnsAsync(new ReminderFunnelDto(1, 1, 1) { Canceled = 0 });

            var controller = new ReminderFunnelController(sessionService.Object, auditLogService.Object);

            var result = await controller.Index(from, new DateTime(2026, 8, 31));

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReminderFunnelDashboardViewModel>(viewResult.Model);
            Assert.Equal(1, model.Funnel.Sent);
            Assert.Equal(1, model.Funnel.Confirmed);
            Assert.Equal(1, model.Funnel.Attended);

            // Solo la sesión 1 se contó (envío real de paciente); la 2 no se envió y la 3 es de cobranza.
            sessionService.Verify(s => s.BuildReminderFunnelAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(1)),
                from,
                toExclusive), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldIgnoreInvalidEntityIds_AndMalformedPayloads()
        {
            var auditLogService = new Mock<IAuditLogService>();
            var sessionService = new Mock<ISessionService>();

            var from = new DateTime(2026, 8, 1);
            var toExclusive = new DateTime(2026, 9, 1);

            var dispatches = new List<AuditLog>
            {
                // EntityId no numérico: se descarta aunque el payload indique envío.
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "abc",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new { DispatchType = "PatientReminder", SessionId = 99, EmailSent = true, WhatsAppSent = false })
                },
                // JSON malformado: WasActuallySent=false, se descarta.
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "4",
                    Action = "Create",
                    NewValuesJson = "not-json"
                },
                // Sin DispatchType (default PatientReminder) pero enviado: se cuenta.
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "5",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new { SessionId = 5, EmailSent = true, WhatsAppSent = false })
                },
                // Enviado por WhatsApp: se cuenta.
                new AuditLog
                {
                    EntityName = "ReminderDispatch",
                    EntityId = "6",
                    Action = "Create",
                    NewValuesJson = JsonSerializer.Serialize(new { DispatchType = "PatientReminder", SessionId = 6, EmailSent = false, WhatsAppSent = true })
                }
            };

            auditLogService
                .Setup(a => a.GetAllAsync("ReminderDispatch", null, null, "Create", from, new DateTime(2026, 8, 31)))
                .ReturnsAsync(dispatches);

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.IsAny<IReadOnlyCollection<int>>(), from, toExclusive))
                .ReturnsAsync(new ReminderFunnelDto(0, 0, 0));

            var controller = new ReminderFunnelController(sessionService.Object, auditLogService.Object);

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
            var auditLogService = new Mock<IAuditLogService>();
            var sessionService = new Mock<ISessionService>();

            // Sin despachos, el funnel queda vacío (ceros).
            auditLogService
                .Setup(a => a.GetAllAsync("ReminderDispatch", null, null, "Create", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Array.Empty<AuditLog>());

            sessionService
                .Setup(s => s.BuildReminderFunnelAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ReminderFunnelDto(0, 0, 0));

            var controller = new ReminderFunnelController(sessionService.Object, auditLogService.Object);

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