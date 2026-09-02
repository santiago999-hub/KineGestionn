using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;

namespace KineGestion.Core.Services
{
    public class EquipmentService : IEquipmentService
    {
        private readonly IEquipmentRepository _repository;

        public EquipmentService(IEquipmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<Equipment?> GetByIdAsync(int id)
            => await _repository.GetByIdAsync(id);

        public async Task<IEnumerable<Equipment>> GetAllAsync()
            => await _repository.GetAllAsync();

        public async Task<(IEnumerable<Equipment> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, int? officeId)
            => await _repository.GetPagedAsync(page, pageSize, search, officeId);

        public async Task<IEnumerable<Equipment>> GetByOfficeIdAsync(int officeId)
            => await _repository.GetByOfficeIdAsync(officeId);

        public async Task<Equipment> CreateAsync(Equipment equipment)
        {
            equipment.Name = NormalizeAndValidateRequired(equipment.Name, nameof(Equipment.Name), "El nombre del equipamiento es obligatorio.");
            await ValidateNameUniquenessAsync(equipment.Name, equipment.OfficeId);
            return await _repository.AddAsync(equipment);
        }

        public async Task<Equipment> UpdateAsync(Equipment equipment)
        {
            equipment.Name = NormalizeAndValidateRequired(equipment.Name, nameof(Equipment.Name), "El nombre del equipamiento es obligatorio.");
            await ValidateNameUniquenessAsync(equipment.Name, equipment.OfficeId, excludeId: equipment.Id);
            return await _repository.UpdateAsync(equipment);
        }

        public async Task DeleteAsync(int id)
        {
            var equipment = await _repository.GetByIdAsync(id);
            if (equipment is null)
                throw new BusinessValidationException("El equipamiento no existe.", string.Empty);

            await _repository.DeleteAsync(id);
        }

        private async Task ValidateNameUniquenessAsync(string name, int? officeId, int? excludeId = null)
        {
            bool existe = await _repository.ExistsByNameAsync(name, officeId, excludeId);
            if (existe)
            {
                var scope = officeId.HasValue ? "en este consultorio" : "sin asignar";
                throw new BusinessValidationException(
                    $"Ya existe un equipamiento con el nombre '{name}' {scope}.",
                    nameof(Equipment.Name));
            }
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