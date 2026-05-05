using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface ISessionService
    {
        Task<Session?> GetByIdAsync(int id);
        /// <summary>
        /// OBSOLETO: Carga TODAS las sesiones incluyendo nav properties Patient, Professional, Treatment y Office.
        /// Con 5.000 sesiones, EF materializa ~20.000 objetos en heap. Usar GetPagedListForAdminAsync.
        /// </summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync con pageNumber y pageSize.")]
        Task<IEnumerable<Session>> GetAllForAdminAsync();
        /// <summary>
        /// OBSOLETO: Delega a GetPagedForAdminAsync del repo (4 JOINs + entidades completas en memoria).
        /// Usar GetPagedListForAdminAsync: proyección SQL con solo los campos necesarios para la tabla.
        /// </summary>
        [Obsolete("Carga entidades completas con 4 JOINs. Usar GetPagedListForAdminAsync.")]
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListForAdminAsync(int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus, string? sortBy, string? sortDir);
        /// <summary>Carga entidades completas con 3 JOINs. Usar <see cref="GetPagedListByProfessionalAsync"/>.</summary>
        [Obsolete("Carga entidades completas con 3 JOINs. Usar GetPagedListByProfessionalAsync.")]
        Task<(IEnumerable<Session> Sessions, int TotalCount)> GetPagedByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus);
        Task<(IEnumerable<SessionListDto> Items, int TotalCount)> GetPagedListByProfessionalAsync(int professionalId, int page, int pageSize, string? search, SessionStatus? status, PaymentStatus? paymentStatus);
        /// <summary>OBSOLETO: Igual que GetAllForAdminAsync. Riesgo de OOM. Usar métodos paginados.</summary>
        [Obsolete("Peligro de Memory Bomb. Usar GetPagedListForAdminAsync o GetPagedListByProfessionalAsync.")]
        Task<IEnumerable<Session>> GetAllAsync();
        Task<IEnumerable<Session>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Session>> GetByProfessionalIdAsync(int professionalId);
        Task<int> CountAsync();
        Task<int> CountByTreatmentIdAsync(int treatmentId);
        Task<int> CountByPatientIdAsync(int patientId);
        Task<int> CountByProfessionalIdAsync(int professionalId);
        Task<int> CountByOfficeIdAsync(int officeId);
        Task<Session> CreateAsync(Session session);
        Task<Session> UpdateAsync(Session session);
        Task DeleteAsync(int id);
    }
}
