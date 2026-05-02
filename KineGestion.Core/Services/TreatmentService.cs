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

        public async Task<IEnumerable<Treatment>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

        public async Task<(IEnumerable<Treatment> Treatments, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
            => await _repository.GetPagedAsync(page, pageSize, search);

        public async Task<(IEnumerable<TreatmentListDto> Items, int TotalCount)> GetPagedListAsync(int page, int pageSize, string? search)
            => await _repository.GetPagedListAsync(page, pageSize, search);

        public async Task<IEnumerable<TreatmentSelectDto>> GetForSelectAsync()
            => await _repository.GetForSelectAsync();

        public async Task<IEnumerable<TreatmentSelectDto>> GetByPatientForSelectAsync(int patientId)
            => await _repository.GetByPatientForSelectAsync(patientId);

        public async Task<int> CountAsync()
            => await _repository.CountAsync();

        public async Task<int> CountByPatientIdAsync(int patientId)
            => await _repository.CountByPatientIdAsync(patientId);

        public async Task<Treatment> CreateAsync(Treatment treatment)
        {
            ValidateTreatment(treatment);
            return await _repository.AddAsync(treatment);
        }

        public async Task<Treatment> UpdateAsync(Treatment treatment)
        {
            ValidateTreatment(treatment);
            return await _repository.UpdateAsync(treatment);
        }

        public async Task DeleteAsync(int id)
        {
            int sesiones = await _sessionRepository.CountByTreatmentIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el tratamiento porque tiene {sesiones} sesión(es) asociada(s).",
                    string.Empty);

            await _repository.DeleteAsync(id);
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
    }
}
