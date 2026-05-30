using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class ProfessionalService : IProfessionalService
    {
        private readonly IProfessionalRepository _repository;
        private readonly ISessionRepository _sessionRepository;

        public ProfessionalService(IProfessionalRepository repository, ISessionRepository sessionRepository)
        {
            _repository = repository;
            _sessionRepository = sessionRepository;
        }

        public async Task<Professional?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync (listados) o GetForSelectAsync (dropdowns).")]
        public async Task<IEnumerable<Professional>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Professional>> GetActiveProfessionalsAsync()
            => await _repository.GetActivosAsync();

        public async Task<IEnumerable<ProfessionalSelectDto>> GetForSelectAsync()
            => await QueryCache.GetOrCreateAsync(
                "professionals:select:active",
                () => _repository.GetForSelectAsync(),
                TimeSpan.FromSeconds(30));

        public async Task<(IEnumerable<Professional> Professionals, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
            => await QueryCache.GetOrCreateAsync(
                $"professionals:paged:{page}:{pageSize}:{NormalizeSearch(search)}",
                () => _repository.GetPagedAsync(page, pageSize, search),
                TimeSpan.FromSeconds(10));

        public async Task<int> CountActiveAsync()
            => await QueryCache.GetOrCreateAsync(
                "professionals:active:count",
                () => _repository.CountActiveAsync(),
                TimeSpan.FromSeconds(15));

        public async Task ValidateMatriculaUniquenessAsync(string matricula, int? excludeId = null)
        {
            bool existe = await _repository.ExistsByMatriculaAsync(matricula, excludeId);
            if (existe)
                throw new BusinessValidationException(
                    $"La matrícula '{matricula}' ya se encuentra registrada en el sistema.",
                    nameof(Professional.Matricula));
        }

        public async Task<Professional> CreateAsync(Professional professional)
        {
            professional.Matricula = NormalizeAndValidateRequired(professional.Matricula, nameof(Professional.Matricula), "La matrícula es obligatoria.");
            await ValidateMatriculaUniquenessAsync(professional.Matricula);
            professional.IsActivo = true;
            var created = await _repository.AddAsync(professional);
            QueryCache.InvalidatePrefix("professionals:");
            return created;
        }

        public async Task<Professional> UpdateAsync(Professional professional)
        {
            professional.Matricula = NormalizeAndValidateRequired(professional.Matricula, nameof(Professional.Matricula), "La matrícula es obligatoria.");
            await ValidateMatriculaUniquenessAsync(professional.Matricula, excludeId: professional.Id);
            var updated = await _repository.UpdateAsync(professional);
            QueryCache.InvalidatePrefix("professionals:");
            return updated;
        }

        public async Task DeleteAsync(int id)
        {
            int sesiones = await _sessionRepository.CountByProfessionalIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el profesional porque tiene {sesiones} sesión(es) registrada(s).",
                    string.Empty);

            await _repository.DeleteAsync(id);
            QueryCache.InvalidatePrefix("professionals:");
        }

        private static string NormalizeAndValidateRequired(string? value, string propertyName, string errorMessage)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new BusinessValidationException(errorMessage, propertyName);

            return normalized;
        }

        private static string NormalizeSearch(string? search)
            => string.IsNullOrWhiteSpace(search) ? "_" : search.Trim().ToLowerInvariant();
    }
}
