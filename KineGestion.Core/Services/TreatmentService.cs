using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class TreatmentService : ITreatmentService
    {
        private readonly ITreatmentRepository _repository;
        private readonly ISessionRepository _sessionRepository;

        public TreatmentService(ITreatmentRepository repository, ISessionRepository sessionRepository)
        {
            _repository = repository;
            _sessionRepository = sessionRepository;
        }

        public async Task<Treatment?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        [Obsolete("Carga toda la tabla con nav properties en memoria. Usar GetPagedListAsync.")]
        public async Task<IEnumerable<Treatment>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        [Obsolete("Carga entidades con Patient + Sesiones en memoria. Usar GetPagedListAsync.")]
        public async Task<(IEnumerable<Treatment> Treatments, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
#pragma warning disable CS0618
            => await _repository.GetPagedAsync(page, pageSize, search);
#pragma warning restore CS0618

        public async Task<(IEnumerable<TreatmentListDto> Items, int TotalCount)> GetPagedListAsync(int page, int pageSize, string? search)
            => await QueryCache.GetOrCreateAsync(
                $"treatments:paged:{page}:{pageSize}:{NormalizeSearch(search)}",
                () => _repository.GetPagedListAsync(page, pageSize, search),
                TimeSpan.FromSeconds(10));

        public async Task<IEnumerable<TreatmentSelectDto>> GetForSelectAsync()
            => await QueryCache.GetOrCreateAsync(
                "treatments:select:active",
                () => _repository.GetForSelectAsync(),
                TimeSpan.FromSeconds(30));

        public async Task<IEnumerable<TreatmentSelectDto>> GetByPatientForSelectAsync(int patientId)
            => await QueryCache.GetOrCreateAsync(
                $"treatments:select:patient:{patientId}",
                () => _repository.GetByPatientForSelectAsync(patientId),
                TimeSpan.FromSeconds(20));

        public async Task<int> CountAsync()
            => await QueryCache.GetOrCreateAsync(
                "treatments:count",
                () => _repository.CountAsync(),
                TimeSpan.FromSeconds(15));

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await QueryCache.GetOrCreateAsync(
                $"treatments:count:patient:{patientId}",
                () => _repository.CountByPatientIdAsync(patientId),
                TimeSpan.FromSeconds(15));

        public async Task<Treatment> CreateAsync(Treatment treatment)
        {
            ValidateTreatment(treatment);
            var created = await _repository.AddAsync(treatment);
            QueryCache.InvalidatePrefix("treatments:");
            return created;
        }

        public async Task<Treatment> UpdateAsync(Treatment treatment)
        {
            ValidateTreatment(treatment);
            var updated = await _repository.UpdateAsync(treatment);
            QueryCache.InvalidatePrefix("treatments:");
            return updated;
        }

        public async Task DeleteAsync(int id)
        {
            int sesiones = await _sessionRepository.CountByTreatmentIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el tratamiento porque tiene {sesiones} sesión(es) asociada(s).",
                    string.Empty);

            await _repository.DeleteAsync(id);
            QueryCache.InvalidatePrefix("treatments:");
        }

        private static void ValidateTreatment(Treatment treatment)
        {
            if (treatment.CantidadSesionesTotales < 1)
                throw new BusinessValidationException(
                    "La cantidad de sesiones debe ser al menos 1.",
                    nameof(Treatment.CantidadSesionesTotales));

            if (treatment.FechaInicio == default)
                throw new BusinessValidationException(
                    "La fecha de inicio del tratamiento es obligatoria.",
                    nameof(Treatment.FechaInicio));
        }

        private static string NormalizeSearch(string? search)
            => string.IsNullOrWhiteSpace(search) ? "_" : search.Trim().ToLowerInvariant();
    }
}
