using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
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

        public async Task<IEnumerable<Treatment>> GetByPatientIdAsync(int patientId)
            => await _repository.GetByPatientIdAsync(patientId);

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
            => await _repository.DeleteAsync(id);

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
