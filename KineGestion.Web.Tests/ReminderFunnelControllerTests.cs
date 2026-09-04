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
    }
}