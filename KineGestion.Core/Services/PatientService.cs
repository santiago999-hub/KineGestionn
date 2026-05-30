using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
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

        [Obsolete("Carga toda la tabla en memoria. Usar GetPagedAsync (listados) o GetForSelectAsync (dropdowns).")]
        public async Task<IEnumerable<Patient>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<PatientSelectDto>> GetForSelectAsync()
            => await QueryCache.GetOrCreateAsync(
                "patients:select:active",
                () => _repository.GetForSelectAsync(),
                TimeSpan.FromSeconds(30));

        /// <summary>
        /// LÓGICA DE NEGOCIO: filtra pacientes activos.
        /// Esta decisión vive en el Service, no en el Controller ni en el Repository.
        /// </summary>
        public async Task<IEnumerable<Patient>> GetActivePatientsAsync()
            => await QueryCache.GetOrCreateAsync(
                "patients:active:list",
                () => _repository.GetActivosAsync(),
                TimeSpan.FromSeconds(20));

        public async Task<(IEnumerable<Patient> Patients, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
            => await QueryCache.GetOrCreateAsync(
                $"patients:paged:{page}:{pageSize}:{NormalizeSearch(search)}",
                () => _repository.GetPagedAsync(page, pageSize, search),
                TimeSpan.FromSeconds(10));

        public async Task<int> CountActiveAsync()
            => await QueryCache.GetOrCreateAsync(
                "patients:active:count",
                () => _repository.CountActiveAsync(),
                TimeSpan.FromSeconds(15));

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
                patient.DNI = NormalizeAndValidateRequired(patient.DNI, nameof(Patient.DNI), "El DNI es obligatorio.");
                await ValidateDniUniquenessAsync(patient.DNI);
            var created = await _repository.AddAsync(patient);
            QueryCache.InvalidatePrefix("patients:");
            return created;
        }

        /// <summary>Valida unicidad de DNI excluyendo al propio paciente (caso edición).</summary>
        public async Task<Patient> UpdateAsync(Patient patient)
        {
                ValidateFechaNacimiento(patient.FechaNacimiento);
                patient.DNI = NormalizeAndValidateRequired(patient.DNI, nameof(Patient.DNI), "El DNI es obligatorio.");
                await ValidateDniUniquenessAsync(patient.DNI, excludeId: patient.Id);
            var updated = await _repository.UpdateAsync(patient);
            QueryCache.InvalidatePrefix("patients:");
            return updated;
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
            QueryCache.InvalidatePrefix("patients:");
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

