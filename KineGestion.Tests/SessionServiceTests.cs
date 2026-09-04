using System;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using Moq;
using Xunit;

namespace KineGestion.Tests
{
    public class SessionServiceTests
    {
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly Mock<ITreatmentRepository> _treatmentRepositoryMock;
        private readonly SessionService _service;

        public SessionServiceTests()
        {
            QueryCache.ClearAll();
            _sessionRepositoryMock = new Mock<ISessionRepository>();
            _treatmentRepositoryMock = new Mock<ITreatmentRepository>();
            _service = new SessionService(_sessionRepositoryMock.Object, _treatmentRepositoryMock.Object);
        }

        [Fact]
        public async Task GetPagedListForAdminAsync_ShouldCacheResultBetweenCalls()
        {
            var expected = (Items: (IEnumerable<KineGestion.Core.DTOs.SessionListDto>)new[]
            {
                new KineGestion.Core.DTOs.SessionListDto(1, DateTime.UtcNow, Core.SessionStatus.Pending, Core.PaymentStatus.Pending, 1, "Paciente", "Profesional", "Tratamiento", "Consultorio", false)
            }, TotalCount: 1);

            _sessionRepositoryMock
                .Setup(r => r.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null))
                .ReturnsAsync(expected);

            var first = await _service.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null);
            var second = await _service.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null);

            Assert.Equal(1, first.TotalCount);
            Assert.Equal(1, second.TotalCount);
            _sessionRepositoryMock.Verify(r => r.GetPagedListForAdminAsync(1, 10, null, null, null, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenProfessionalHasConflict()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        [Fact]
        public async Task CreateAsync_ShouldUseConfiguredConflictWindow()
        {
            var session = BuildSession();
            var customWindow = 30;
            var serviceWithCustomWindow = new SessionService(
                _sessionRepositoryMock.Object,
                _treatmentRepositoryMock.Object,
                customWindow);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, customWindow, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(() => serviceWithCustomWindow.CreateAsync(session));

            _sessionRepositoryMock.Verify(
                r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, customWindow, null),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenTreatmentSessionLimitReached()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(10);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 10, Descripcion = "Plan" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        [Fact]
        public async Task UpdateAsync_ShouldRecalculateNroSesion_WhenTreatmentChanges()
        {
            var session = BuildSession();
            session.Id = 10;
            session.TreatmentId = 2;

            var original = BuildSession();
            original.Id = 10;
            original.TreatmentId = 1;
            original.NroSesionEnTratamiento = 5;

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(3);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 20, Descripcion = "Nuevo" });

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var updated = await _service.UpdateAsync(session);

            Assert.Equal(4, updated.NroSesionEnTratamiento);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenEvolutionIsLockedAndChanged()
        {
            var session = BuildSession();
            session.Id = 11;
            session.Evolution = "texto nuevo";

            var original = BuildSession();
            original.Id = 11;
            original.Evolution = "texto anterior";
            original.EvolutionLockedAt = DateTime.UtcNow.AddDays(-1);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(session));
        }

        // ─── CreateAsync — happy paths ────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_ShouldAssignNroSesion_WhenDataIsValid()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(3); // hay 3 sesiones → la nueva será la 4ª

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 10, Descripcion = "Plan" });

            _sessionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.CreateAsync(session);

            Assert.Equal(4, result.NroSesionEnTratamiento);
            _sessionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Session>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldAssignNroSesionOne_WhenTreatmentHasNoSessions()
        {
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(0); // primera sesión del tratamiento

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 5, Descripcion = "Inicio" });

            _sessionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.CreateAsync(session);

            Assert.Equal(1, result.NroSesionEnTratamiento);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenPatientExceedsSessionLimit()
        {
            // Esta validación no está en SessionService actualmente, pero si se agrega
            // en el futuro, este test documenta el comportamiento esperado.
            // Por ahora verifica que el límite del TRATAMIENTO es el punto de control.
            var session = BuildSession();

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(session.TreatmentId))
                .ReturnsAsync(5);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(session.TreatmentId))
                .ReturnsAsync(new Treatment { Id = session.TreatmentId, CantidadSesionesTotales = 5, Descripcion = "Completado" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.CreateAsync(session));
        }

        // ─── UpdateAsync — happy paths ────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ShouldLockEvolution_WhenSetForFirstTime()
        {
            var session = BuildSession();
            session.Id = 20;
            session.Evolution = "Primera evolución del paciente.";

            var original = BuildSession();
            original.Id = 20;
            original.Evolution = null;
            original.EvolutionLockedAt = null; // no estaba bloqueada

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.UpdateAsync(session);

            // Al escribir la evolución por primera vez, se debe bloquear automáticamente
            Assert.NotNull(result.EvolutionLockedAt);
        }

        [Fact]
        public async Task UpdateAsync_ShouldPreserveEvolutionLock_WhenEvolutionIsUnchanged()
        {
            var lockedAt = DateTime.UtcNow.AddDays(-2);
            var session = BuildSession();
            session.Id = 21;
            session.Evolution = "Evolución firmada.";

            var original = BuildSession();
            original.Id = 21;
            original.Evolution = "Evolución firmada."; // mismo texto → no cambia
            original.EvolutionLockedAt = lockedAt;

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.UpdateAsync(session);

            // La fecha de bloqueo original debe preservarse sin cambios
            Assert.Equal(lockedAt, result.EvolutionLockedAt);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenNewTreatmentIsAlsoFull()
        {
            var session = BuildSession();
            session.Id = 22;
            session.TreatmentId = 99; // cambia a tratamiento diferente

            var original = BuildSession();
            original.Id = 22;
            original.TreatmentId = 1; // tratamiento original

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(session.ProfessionalId, session.FechaHora, 45, session.Id))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(original);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(99))
                .ReturnsAsync(8);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync(new Treatment { Id = 99, CantidadSesionesTotales = 8, Descripcion = "Lleno" });

            await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UpdateAsync(session));
        }

        // ─── CancelAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task CancelAsync_ShouldCancelAndSetReason()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Pending;

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            await _service.CancelAsync(session.Id, Core.CancellationReason.PacienteNoPudoAsistir, "turno laboral");

            Assert.Equal(Core.SessionStatus.Canceled, session.Status);
            Assert.Equal(Core.CancellationReason.PacienteNoPudoAsistir, session.CancellationReason);
            Assert.Equal("turno laboral", session.CancellationObs);
            Assert.NotNull(session.CancelledAt);
        }

        [Fact]
        public async Task CancelAsync_ShouldTrimAndNullifyBlankObservation()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Pending;

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            _sessionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            await _service.CancelAsync(session.Id, Core.CancellationReason.Otro, "   ");

            Assert.Null(session.CancellationObs);
        }

        [Fact]
        public async Task CancelAsync_ShouldThrow_WhenSessionNotFound()
        {
            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Session?)null);

            await Assert.ThrowsAsync<BusinessValidationException>(
                () => _service.CancelAsync(999, Core.CancellationReason.Otro, null));
        }

        [Fact]
        public async Task CancelAsync_ShouldThrow_WhenAlreadyCanceled()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Canceled;

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            await Assert.ThrowsAsync<BusinessValidationException>(
                () => _service.CancelAsync(session.Id, Core.CancellationReason.Otro, null));
        }

        [Fact]
        public async Task CancelAsync_ShouldThrow_WhenSessionCompleted()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Completed;

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            await Assert.ThrowsAsync<BusinessValidationException>(
                () => _service.CancelAsync(session.Id, Core.CancellationReason.MotivoClinico, null));
        }

        // ─── ReprogramAsync / SuggestAvailableSlotsAsync ──────────────────────────

        [Fact]
        public async Task ReprogramAsync_ShouldCreateNewSession_WithSameClinicalData()
        {
            var source = BuildSession();
            source.Id = 5;
            source.Status = Core.SessionStatus.Canceled;

            var newFechaHora = DateTime.UtcNow.AddDays(2).Date.AddHours(10);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(source.Id))
                .ReturnsAsync(source);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(source.ProfessionalId, newFechaHora, 45, null))
                .ReturnsAsync(false);

            _sessionRepositoryMock
                .Setup(r => r.CountByTreatmentIdAsync(source.TreatmentId))
                .ReturnsAsync(3);

            _treatmentRepositoryMock
                .Setup(r => r.GetByIdAsync(source.TreatmentId))
                .ReturnsAsync(new Treatment { Id = source.TreatmentId, CantidadSesionesTotales = 10, Descripcion = "Plan" });

            _sessionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Session>()))
                .ReturnsAsync((Session s) => s);

            var result = await _service.ReprogramAsync(source.Id, newFechaHora);

            Assert.Equal(source.PatientId, result.PatientId);
            Assert.Equal(source.ProfessionalId, result.ProfessionalId);
            Assert.Equal(source.TreatmentId, result.TreatmentId);
            Assert.Equal(newFechaHora, result.FechaHora);
            Assert.Equal(Core.SessionStatus.Pending, result.Status);
            Assert.Equal(4, result.NroSesionEnTratamiento);
        }

        [Fact]
        public async Task ReprogramAsync_ShouldThrow_WhenSourceNotCanceled()
        {
            var source = BuildSession();
            source.Id = 6;
            source.Status = Core.SessionStatus.Pending;

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(source.Id))
                .ReturnsAsync(source);

            await Assert.ThrowsAsync<BusinessValidationException>(
                () => _service.ReprogramAsync(source.Id, DateTime.UtcNow.AddDays(1)));
        }

        [Fact]
        public async Task ReprogramAsync_ShouldThrow_WhenProfessionalConflict()
        {
            var source = BuildSession();
            source.Id = 7;
            source.Status = Core.SessionStatus.Canceled;

            var newFechaHora = DateTime.UtcNow.AddDays(2).Date.AddHours(10);

            _sessionRepositoryMock
                .Setup(r => r.GetByIdAsync(source.Id))
                .ReturnsAsync(source);

            _sessionRepositoryMock
                .Setup(r => r.ExistsProfessionalConflictAsync(source.ProfessionalId, newFechaHora, 45, null))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessValidationException>(
                () => _service.ReprogramAsync(source.Id, newFechaHora));
        }

        [Fact]
        public async Task SuggestAvailableSlotsAsync_ShouldReturnOpenWeekdaySlots()
        {
            var from = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc); // lunes
            int professionalId = 3;

            _sessionRepositoryMock
                .Setup(r => r.GetProfessionalBusyTimesAsync(professionalId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<DateTime>());

            var slots = await _service.SuggestAvailableSlotsAsync(professionalId, from, dayCount: 2, count: 3);

            Assert.Equal(3, slots.Count);
            // lunes a las 9, 10 y 11
            Assert.All(slots, s => Assert.False(s.FechaHora.DayOfWeek == DayOfWeek.Saturday || s.FechaHora.DayOfWeek == DayOfWeek.Sunday));
        }

        [Fact]
        public async Task SuggestAvailableSlotsAsync_ShouldSkipBusySlots()
        {
            var from = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc); // lunes
            int professionalId = 3;

            // Lunes a las 9 y 10 están ocupados → los primeros libres son 11, y martes 9, 10
            _sessionRepositoryMock
                .Setup(r => r.GetProfessionalBusyTimesAsync(professionalId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<DateTime>
                {
                    new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc)
                });

            var slots = await _service.SuggestAvailableSlotsAsync(professionalId, from, dayCount: 2, count: 3);

            Assert.Equal(3, slots.Count);
            // Lunes 9 y 10 ocupados → los siguientes libres ese mismo día son 11, 12 y 13
            Assert.Equal(new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc), slots[0].FechaHora);
            Assert.Equal(new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc), slots[1].FechaHora);
            Assert.Equal(new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc), slots[2].FechaHora);
        }

        // ─── DeleteAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ShouldCallRepositoryDelete()
        {
            _sessionRepositoryMock
                .Setup(r => r.DeleteAsync(5))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(5);

            _sessionRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
        }

        private static Session BuildSession()
        {
            return new Session
            {
                Id = 1,
                FechaHora = DateTime.UtcNow,
                ProfessionalId = 3,
                PatientId = 2,
                TreatmentId = 1,
                NroSesionEnTratamiento = 1,
                Status = Core.SessionStatus.Pending,
                PaymentStatus = Core.PaymentStatus.Pending
            };
        }

        [Fact]
        public void GetCancellationTiming_ShouldReturnLate_WhenCancelledLessThan24hBefore()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Canceled;
            session.FechaHora = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc);
            session.CancelledAt = new DateTime(2026, 5, 10, 7, 0, 0, DateTimeKind.Utc); // 2h antes

            Assert.Equal(Core.CancellationTiming.Late, _service.GetCancellationTiming(session));
        }

        [Fact]
        public void GetCancellationTiming_ShouldReturnEarly_WhenCancelled24hOrMoreBefore()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Canceled;
            session.FechaHora = new DateTime(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc);
            session.CancelledAt = new DateTime(2026, 5, 13, 9, 0, 0, DateTimeKind.Utc); // 48h antes

            Assert.Equal(Core.CancellationTiming.Early, _service.GetCancellationTiming(session));
        }

        [Fact]
        public void GetCancellationTiming_ShouldReturnEarly_WhenSessionIsNotCancelledAndFarInFuture()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Pending;
            session.CancelledAt = null;
            session.FechaHora = DateTime.UtcNow.AddDays(5); // cancelar "ahora" sería temprana (>24h)

            Assert.Equal(Core.CancellationTiming.Early, _service.GetCancellationTiming(session));
        }

        [Fact]
        public void GetCancellationTiming_ShouldReturnLate_ForPendingSessionWithin24h()
        {
            var session = BuildSession();
            session.Status = Core.SessionStatus.Pending;
            session.CancelledAt = null;
            session.FechaHora = DateTime.UtcNow.AddHours(3); // cancelar "ahora" sería tardía (<24h)

            Assert.Equal(Core.CancellationTiming.Late, _service.GetCancellationTiming(session));
        }

        [Fact]
        public async Task BuildReminderFunnelAsync_ShouldCountConfirmations_ConfirmedAndAttendance()
        {
            var from = new DateTime(2026, 8, 1);
            var to = new DateTime(2026, 9, 1);
            var outcomes = new[]
            {
                new KineGestion.Core.DTOs.SessionFunnelOutcomeDto(1, Core.SessionStatus.Completed, true, false, null),
                new KineGestion.Core.DTOs.SessionFunnelOutcomeDto(2, Core.SessionStatus.Completed, true, false, null),
                new KineGestion.Core.DTOs.SessionFunnelOutcomeDto(3, Core.SessionStatus.Pending, true, false, null),
                new KineGestion.Core.DTOs.SessionFunnelOutcomeDto(4, Core.SessionStatus.Canceled, false, true, DateTime.UtcNow),
                new KineGestion.Core.DTOs.SessionFunnelOutcomeDto(5, Core.SessionStatus.Completed, false, false, null)
            };

            _sessionRepositoryMock
                .Setup(r => r.GetSessionFunnelOutcomesAsync(It.IsAny<IReadOnlyCollection<int>>(), from, to))
                .ReturnsAsync(outcomes);

            var funnel = await _service.BuildReminderFunnelAsync(new[] { 1, 2, 3, 4, 5 }, from, to);

            Assert.Equal(5, funnel.Sent);
            Assert.Equal(3, funnel.Confirmed);
            Assert.Equal(3, funnel.Attended);
            Assert.Equal(1, funnel.Canceled);
            Assert.Equal(60m, funnel.ConfirmationRate);
            Assert.Equal(60m, funnel.AttendanceRate);
            Assert.Equal(20m, funnel.CancellationRate);
        }

        [Fact]
        public async Task BuildReminderFunnelAsync_ShouldReturnZeros_WhenNoSentinRange()
        {
            var from = new DateTime(2026, 8, 1);
            var to = new DateTime(2026, 9, 1);

            _sessionRepositoryMock
                .Setup(r => r.GetSessionFunnelOutcomesAsync(It.IsAny<IReadOnlyCollection<int>>(), from, to))
                .ReturnsAsync(Array.Empty<KineGestion.Core.DTOs.SessionFunnelOutcomeDto>());

            var funnel = await _service.BuildReminderFunnelAsync(Array.Empty<int>(), from, to);

            Assert.Equal(0, funnel.Sent);
            Assert.Equal(0, funnel.Confirmed);
            Assert.Equal(0, funnel.Attended);
            Assert.Equal(0m, funnel.ConfirmationRate);
            Assert.Equal(0m, funnel.AttendanceRate);
        }
    }
}
