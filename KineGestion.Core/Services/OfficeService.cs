using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class OfficeService : IOfficeService
    {
        private readonly IOfficeRepository _repository;

        public OfficeService(IOfficeRepository repository)
        {
            _repository = repository;
        }

        public async Task<Office?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Office>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Office>> GetActiveAsync()
            => await _repository.GetActiveAsync();

        public async Task<Office> CreateAsync(Office office)
            => await _repository.AddAsync(office);

        public async Task<Office> UpdateAsync(Office office)
            => await _repository.UpdateAsync(office);

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);
    }
}
