using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _repository;

        public SessionService(ISessionRepository repository)
        {
            _repository = repository;
        }

        public async Task<Session?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Session>> GetAllForAdminAsync()
        {
            var sessions = await _repository.GetAllAsync();
            foreach (var s in sessions)
                s.Evolution = null;
            return sessions;
        }

        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<Session> CreateAsync(Session session)
        {
            await ValidateProfessionalAvailabilityAsync(session.ProfessionalId, session.FechaHora);
            int sesionesExistentes = await _repository.CountByTreatmentIdAsync(session.TreatmentId);
            session.NroSesionEnTratamiento = sesionesExistentes + 1;
            return await _repository.AddAsync(session);
        }

        public async Task<Session> UpdateAsync(Session session)
        {
            await ValidateProfessionalAvailabilityAsync(session.ProfessionalId, session.FechaHora, session.Id);
            return await _repository.UpdateAsync(session);
        }

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);

        private async Task ValidateProfessionalAvailabilityAsync(int professionalId, DateTime fechaHora, int? excludeSessionId = null)
        {
            bool hasConflict = await _repository.ExistsProfessionalConflictAsync(
                professionalId,
                fechaHora,
                windowInMinutes: 45,
                excludeSessionId: excludeSessionId);

            if (hasConflict)
            {
                throw new BusinessValidationException(
                    "El profesional ya tiene una sesion asignada en un rango de +/- 45 minutos para el horario seleccionado.",
                    nameof(Session.FechaHora));
            }
        }
    }
}
