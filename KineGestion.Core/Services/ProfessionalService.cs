using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class ProfessionalService : IProfessionalService
    {
        private readonly IProfessionalRepository _repository;

        public ProfessionalService(IProfessionalRepository repository)
        {
            _repository = repository;
        }

        public async Task<Professional?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Professional>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task ValidateMatriculaUniquenessAsync(string matricula, int? excludeId = null)
        {
            bool existe = await _repository.ExistsByMatriculaAsync(matricula, excludeId);
            if (existe)
                throw new InvalidOperationException(
                    $"La matrícula '{matricula}' ya se encuentra registrada en el sistema.");
        }

        public async Task<Professional> CreateAsync(Professional professional)
        {
            await ValidateMatriculaUniquenessAsync(professional.Matricula);
            return await _repository.AddAsync(professional);
        }

        public async Task<Professional> UpdateAsync(Professional professional)
        {
            await ValidateMatriculaUniquenessAsync(professional.Matricula, excludeId: professional.Id);
            return await _repository.UpdateAsync(professional);
        }

        public async Task DeleteAsync(int id)
            => await _repository.DeleteAsync(id);
    }
}
