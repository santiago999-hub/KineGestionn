using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class OfficeService : IOfficeService
    {
        private readonly IOfficeRepository _repository;
        private readonly ISessionRepository _sessionRepository;

        public OfficeService(IOfficeRepository repository, ISessionRepository sessionRepository)
        {
            _repository = repository;
            _sessionRepository = sessionRepository;
        }

        public async Task<Office?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync o GetActiveAsync.")]
        public async Task<IEnumerable<Office>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Office>> GetActiveAsync()
            => await QueryCache.GetOrCreateAsync(
                "offices:active:list",
                () => _repository.GetActiveAsync(),
                TimeSpan.FromSeconds(20));

        public async Task<(IEnumerable<Office> Offices, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
            => await _repository.GetPagedAsync(page, pageSize, search);

        public async Task<Office> CreateAsync(Office office)
        {
            office.Name = NormalizeAndValidateRequired(office.Name, nameof(Office.Name), "El nombre del consultorio es obligatorio.");
            await ValidateNameUniquenessAsync(office.Name);
            var created = await _repository.AddAsync(office);
            QueryCache.InvalidatePrefix("offices:");
            return created;
        }

        public async Task<Office> UpdateAsync(Office office)
        {
            office.Name = NormalizeAndValidateRequired(office.Name, nameof(Office.Name), "El nombre del consultorio es obligatorio.");
            await ValidateNameUniquenessAsync(office.Name, excludeId: office.Id);
            var updated = await _repository.UpdateAsync(office);
            QueryCache.InvalidatePrefix("offices:");
            return updated;
        }

        public async Task DeleteAsync(int id)
        {
            int sesiones = await _sessionRepository.CountByOfficeIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el consultorio porque tiene {sesiones} sesión(es) asociada(s).",
                    string.Empty);

            await _repository.DeleteAsync(id);
            QueryCache.InvalidatePrefix("offices:");
        }

        private async Task ValidateNameUniquenessAsync(string name, int? excludeId = null)
        {
            bool existe = await _repository.ExistsByNameAsync(name, excludeId);
            if (existe)
                throw new BusinessValidationException(
                    $"Ya existe un consultorio con el nombre '{name}'.",
                    nameof(Office.Name));
        }

        private static string NormalizeAndValidateRequired(string? value, string propertyName, string errorMessage)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new BusinessValidationException(errorMessage, propertyName);

            return normalized;
        }
    }
}
