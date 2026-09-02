using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Models.ViewModels
{
    public class EquipmentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del equipamiento es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Consultorio")]
        public int? OfficeId { get; set; }

        public string? OfficeName { get; set; }

        public IEnumerable<SelectListItem> Offices { get; set; } = new List<SelectListItem>();

        public static EquipmentViewModel FromEntity(Equipment entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            OfficeId = entity.OfficeId,
            OfficeName = entity.Office?.Name
        };

        public Equipment ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            OfficeId = OfficeId
        };
    }

    public class EquipmentIndexViewModel
    {
        public IEnumerable<EquipmentViewModel> Items { get; set; } = new List<EquipmentViewModel>();
        public string? Search { get; set; }
        public int? OfficeId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;
        public IEnumerable<SelectListItem> Offices { get; set; } = new List<SelectListItem>();
    }
}