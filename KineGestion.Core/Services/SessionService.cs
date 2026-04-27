using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
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

        public async Task<IEnumerable<Session>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<Session> CreateAsync(Session session)
            => await _repository.AddAsync(session);

        public async Task<Session> UpdateAsync(Session session)
            => await _repository.UpdateAsync(session);

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);
    }
}
