using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Web.Mapping;
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

        [Display(Name = "Estado de sesión")]
        public SessionStatus Status { get; set; } = SessionStatus.Pending;

        [Display(Name = "Estado de pago")]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [Range(1, 365, ErrorMessage = "El numero de sesion debe ser mayor a 0.")]
        [Display(Name = "Nro. sesion en tratamiento")]
        public int NroSesionEnTratamiento { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un paciente.")]
        [Display(Name = "Paciente")]
        public int PacienteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un profesional.")]
        [Display(Name = "Profesional")]
        public int ProfesionalId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un tratamiento.")]
        [Display(Name = "Tratamiento")]
        public int TratamientoId { get; set; }

        // ── Área clínica sensible ─────────────────────────────────────────────
        /// <summary>
        /// Solo se carga desde el servicio cuando el contexto es profesional/detalle.
        /// En listados administrativos este campo llega siempre null.
        /// </summary>
        [StringLength(4000)]
        [Display(Name = "Evolución clínica")]
        public string? Evolution { get; set; }

        [StringLength(2000)]
        [Display(Name = "Notas internas")]
        public string? InternalNotes { get; set; }

        [Display(Name = "Consultorio")]
        public int? OfficeId { get; set; }

        public string? PacienteNombre { get; set; }
        public string? ProfesionalNombre { get; set; }
        public string? TratamientoDescripcion { get; set; }
        public string? OfficeNombre { get; set; }
        public bool EvolutionBloqueada { get; set; }

        public IEnumerable<SelectListItem> Pacientes { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Profesionales { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Tratamientos { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Consultorios { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> EstadosSesion { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> EstadosPago { get; set; } = new List<SelectListItem>();

        public static SessionViewModel FromEntity(Session session)
            => MappingHelper.ToSessionViewModel(session, includeEvolution: true);

        public static SessionViewModel FromEntityForAdmin(Session session)
            => MappingHelper.ToSessionViewModel(session, includeEvolution: false);

        /// <summary>
        /// Mapeo desde el DTO optimizado del listado. No require nav properties cargadas.
        /// Evolution siempre null (no se muestra en listas).
        /// </summary>
        public static SessionViewModel FromDto(SessionListDto dto) => new()
        {
            Id                     = dto.Id,
            FechaHora              = dto.FechaHora,
            Status                 = dto.Status,
            PaymentStatus          = dto.PaymentStatus,
            NroSesionEnTratamiento = dto.NroSesionEnTratamiento,
            PacienteNombre         = dto.PacienteNombre,
            ProfesionalNombre      = dto.ProfesionalNombre,
            TratamientoDescripcion = dto.TratamientoDescripcion,
            OfficeNombre           = dto.OfficeNombre,
            EvolutionBloqueada     = dto.EvolutionBloqueada
        };

        public Session ToEntity() => MappingHelper.ToSessionEntity(this);
    }
}
