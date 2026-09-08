using System.Linq;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;

namespace KineGestion.Data.Repositories
{
    /// <summary>
    /// Base común de los repositorios de Session: comparte el DbContext inyectado,
    /// el proveedor de usuario actual y las ayudas de alcance por rol.
    /// </summary>
    public abstract class SessionRepositoryBase
    {
        protected readonly AppDbContext _context;
        protected readonly ICurrentUserProvider? _currentUserProvider;

        protected SessionRepositoryBase(AppDbContext context)
        {
            _context = context;
        }

        protected SessionRepositoryBase(AppDbContext context, ICurrentUserProvider currentUserProvider)
        {
            _context = context;
            _currentUserProvider = currentUserProvider;
        }

        protected bool IsClinicalStaff()
            => _currentUserProvider?.IsInRole("Admin") == true
            || _currentUserProvider?.IsInRole("Kinesiologo") == true
            || _currentUserProvider?.IsInRole("Asistente") == true;

        protected bool CanViewClinicalNotes()
            => _currentUserProvider?.IsInRole("Admin") == true
            || _currentUserProvider?.IsInRole("Kinesiologo") == true;

        protected int? GetProfessionalIdFilter()
        {
            if (_currentUserProvider is null) return null;
            if (_currentUserProvider.IsInRole("Admin")) return null;
            var profIdStr = _currentUserProvider.GetClaimValue("ProfessionalId");
            return int.TryParse(profIdStr, out var profId) ? profId : null;
        }

        /// <summary>
        /// Aplica el alcance por rol al query: Admin (o sin usuario) ve todos los registros;
        /// Kinesiologo/Asistente solo ve las sesiones de su profesional. Evita repetir el branch.
        /// </summary>
        protected IQueryable<Session> ApplyCountScope(IQueryable<Session> query)
        {
            var profFilter = GetProfessionalIdFilter();
            return profFilter.HasValue
                ? query.Where(s => s.ProfessionalId == profFilter.Value)
                : query;
        }
    }
}