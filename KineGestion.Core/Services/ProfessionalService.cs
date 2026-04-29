using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public async Task<IEnumerable<Professional>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<IEnumerable<Professional>> GetActiveProfessionalsAsync()
            => await _repository.GetActivosAsync();

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
            await ValidateMatriculaUniquenessAsync(professional.Matricula);
            professional.IsActivo = true;
            return await _repository.AddAsync(professional);
        }

        public async Task<Professional> UpdateAsync(Professional professional)
        {
            await ValidateMatriculaUniquenessAsync(professional.Matricula, excludeId: professional.Id);
            return await _repository.UpdateAsync(professional);
        }

        public async Task DeleteAsync(int id)
        {
            int sesiones = await _sessionRepository.CountByProfessionalIdAsync(id);
            if (sesiones > 0)
                throw new BusinessValidationException(
                    $"No se puede eliminar el profesional porque tiene {sesiones} sesión(es) registrada(s).",
                    string.Empty);

            await _repository.DeleteAsync(id);
        }
    }
}
