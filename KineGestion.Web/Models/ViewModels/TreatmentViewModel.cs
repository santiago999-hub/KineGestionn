using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Models.ViewModels
{
    public class TreatmentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La descripción del tratamiento es obligatoria.")]
        [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(1, 365, ErrorMessage = "La cantidad de sesiones debe estar entre 1 y 365.")]
        [Display(Name = "Cantidad de sesiones")]
        public int CantidadSesionesTotales { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de inicio")]
        public DateTime FechaInicio { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un paciente.")]
        [Display(Name = "Paciente")]
        public int PacienteId { get; set; }

        // Datos de solo lectura para vistas de detalle
        public string? PacienteNombre { get; set; }
        public int SesionesRealizadas { get; set; }

        // SelectList para el formulario
        public IEnumerable<SelectListItem> Pacientes { get; set; } = new List<SelectListItem>();

        public static TreatmentViewModel FromEntity(Treatment treatment) => new()
        {
            Id = treatment.Id,
            Descripcion = treatment.Descripcion,
            CantidadSesionesTotales = treatment.CantidadSesionesTotales,
            FechaInicio = treatment.FechaInicio,
            PacienteId = treatment.PatientId,
            PacienteNombre = treatment.Patient is null
                ? string.Empty
                : $"{treatment.Patient.Apellido}, {treatment.Patient.Nombre}",
            SesionesRealizadas = treatment.Sesiones?.Count ?? 0
        };

        /// <summary>
        /// Mapea desde el DTO optimizado del listado paginado.
        /// No requiere que el objeto Treatment tenga cargada la colección Sesiones.
        /// </summary>
        public static TreatmentViewModel FromDto(TreatmentListDto dto) => new()
        {
            Id = dto.Id,
            Descripcion = dto.Descripcion,
            CantidadSesionesTotales = dto.CantidadSesionesTotales,
            FechaInicio = dto.FechaInicio,
            PacienteId = dto.PatientId,
            PacienteNombre = dto.PatientNombre,
            SesionesRealizadas = dto.SesionesCount
        };

        public Treatment ToEntity() => new()
        {
            Id = Id,
            Descripcion = Descripcion,
            CantidadSesionesTotales = CantidadSesionesTotales,
            FechaInicio = FechaInicio,
            PatientId = PacienteId
        };
    }
}
