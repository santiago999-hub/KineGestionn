using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        [Required(ErrorMessage = "Debe seleccionar un paciente.")]
        [Display(Name = "Paciente")]
        public int PacienteId { get; set; }

        // Datos de solo lectura para vistas de detalle
        public string? PacienteNombre { get; set; }

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
                : $"{treatment.Patient.Apellido}, {treatment.Patient.Nombre}"
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
