using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class TreatmentService : ITreatmentService
    {
        private readonly ITreatmentRepository _repository;

        public TreatmentService(ITreatmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<Treatment?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Treatment>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<Treatment> CreateAsync(Treatment treatment)
            => await _repository.AddAsync(treatment);

        public async Task<Treatment> UpdateAsync(Treatment treatment)
            => await _repository.UpdateAsync(treatment);

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);
    }
}
