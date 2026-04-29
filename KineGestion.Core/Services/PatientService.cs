using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    /// <summary>
    /// Contiene la lógica de negocio pura del dominio Patient.
    /// NO conoce EF Core, SQL ni ningún detalle de infraestructura.
    /// Solo interactúa con IPatientRepository (abstracción).
    /// </summary>
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _repository;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly ISessionRepository _sessionRepository;

        public PatientService(
            IPatientRepository repository,
            ITreatmentRepository treatmentRepository,
            ISessionRepository sessionRepository)
        {
            _repository = repository;
            _treatmentRepository = treatmentRepository;
            _sessionRepository = sessionRepository;
        }

        public async Task<Patient?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Patient>> GetAllAsync()
            => await _repository.GetAllAsync();

        /// <summary>
        /// LÓGICA DE NEGOCIO: filtra pacientes activos.
        /// Esta decisión vive en el Service, no en el Controller ni en el Repository.
        /// </summary>
        public async Task<IEnumerable<Patient>> GetActivePatientsAsync()
            => await _repository.GetActivosAsync();

        /// <summary>
        /// LÓGICA DE NEGOCIO: un DNI no puede estar registrado dos veces.
        /// El Service sabe QUÉ validar; el Repository sabe CÓMO consultarlo.
        /// </summary>
        public async Task ValidateDniUniquenessAsync(string dni, int? excludeId = null)
        {
            bool existe = await _repository.ExistsByDniAsync(dni, excludeId);
            if (existe)
                throw new BusinessValidationException(
                    $"El DNI '{dni}' ya se encuentra registrado en el sistema.",
                    nameof(Patient.DNI));
        }

        /// <summary>Valida unicidad de DNI y luego delega la persistencia al Repository.</summary>
        public async Task<Patient> CreateAsync(Patient patient)
        {
                ValidateFechaNacimiento(patient.FechaNacimiento);
                await ValidateDniUniquenessAsync(patient.DNI);
            return await _repository.AddAsync(patient);
        }

        /// <summary>Valida unicidad de DNI excluyendo al propio paciente (caso edición).</summary>
        public async Task<Patient> UpdateAsync(Patient patient)
        {
                ValidateFechaNacimiento(patient.FechaNacimiento);
                await ValidateDniUniquenessAsync(patient.DNI, excludeId: patient.Id);
            return await _repository.UpdateAsync(patient);
        }

        public async Task DeleteAsync(int id)
        {
            int tratamientos = await _treatmentRepository.CountByPatientIdAsync(id);
            if (tratamientos > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el paciente porque tiene {tratamientos} tratamiento(s) registrado(s). Elimine primero los tratamientos asociados.",
                    string.Empty);

            int sesiones = await _sessionRepository.CountByPatientIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el paciente porque tiene {sesiones} sesión(es) registrada(s). Elimine primero las sesiones asociadas.",
                    string.Empty);

            await _repository.DeleteAsync(id);
        }

            /// <summary>
            /// LÓGICA DE NEGOCIO: la fecha de nacimiento no puede ser futura.
            /// Un paciente no puede nacer mañana.
            /// </summary>
            private static void ValidateFechaNacimiento(DateTime fechaNacimiento)
            {
                if (fechaNacimiento.Date >= DateTime.Today)
                    throw new BusinessValidationException(
                        "La fecha de nacimiento no puede ser igual o posterior a la fecha actual.",
                        nameof(Patient.FechaNacimiento));
            }
    }
}

