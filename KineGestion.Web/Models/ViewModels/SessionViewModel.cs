using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Models.ViewModels
{
    public class SessionViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La fecha y hora de la sesion es obligatoria.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Fecha y hora")]
        public DateTime FechaHora { get; set; }

        [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar los 1000 caracteres.")]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        [Display(Name = "Estado de pago")]
        public bool EstadoPago { get; set; }

        [Range(1, 365, ErrorMessage = "El numero de sesion debe ser mayor a 0.")]
        [Display(Name = "Nro. sesion en tratamiento")]
        public int NroSesionEnTratamiento { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un paciente.")]
        [Display(Name = "Paciente")]
        public int PacienteId { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un profesional.")]
        [Display(Name = "Profesional")]
        public int ProfesionalId { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un tratamiento.")]
        [Display(Name = "Tratamiento")]
        public int TratamientoId { get; set; }

        public string? PacienteNombre { get; set; }
        public string? ProfesionalNombre { get; set; }
        public string? TratamientoDescripcion { get; set; }

        public IEnumerable<SelectListItem> Pacientes { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Profesionales { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Tratamientos { get; set; } = new List<SelectListItem>();

        public static SessionViewModel FromEntity(Session session) => new()
        {
            Id = session.Id,
            FechaHora = session.FechaHora,
            Observaciones = session.Observaciones,
            EstadoPago = session.EstadoPago,
            NroSesionEnTratamiento = session.NroSesionEnTratamiento,
            PacienteId = session.PatientId,
            ProfesionalId = session.ProfessionalId,
            TratamientoId = session.TreatmentId,
            PacienteNombre = session.Patient is null ? string.Empty : $"{session.Patient.Apellido}, {session.Patient.Nombre}",
            ProfesionalNombre = session.Professional is null ? string.Empty : $"{session.Professional.Apellido}, {session.Professional.Nombre}",
            TratamientoDescripcion = session.Treatment?.Descripcion
        };

        public Session ToEntity() => new()
        {
            Id = Id,
            FechaHora = FechaHora,
            Observaciones = Observaciones,
            EstadoPago = EstadoPago,
            NroSesionEnTratamiento = NroSesionEnTratamiento,
            PatientId = PacienteId,
            ProfessionalId = ProfesionalId,
            TreatmentId = TratamientoId
        };
    }
}
