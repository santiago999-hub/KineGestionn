using KineGestion.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace KineGestion.Web.Controllers
{
    /// <summary>
    /// Base de los controllers con operaciones de escritura de dominio.
    /// Centraliza el mapeo de errores de validación de negocio (BusinessValidationException)
    /// a errores de ModelState, evitando repetir la normalización de key en cada action.
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Agrega el error de validación de negocio al ModelState usando la propiedad indicada
        /// por la excepción. Si la excepción no trae propiedad, usa <paramref name="fallbackKey"/>
        /// (key de formulario donde mostrar el error).
        /// </summary>
        protected void AddModelStateError(BusinessValidationException ex, string? fallbackKey = null)
        {
            var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? fallbackKey ?? string.Empty : ex.PropertyName;
            ModelState.AddModelError(key, ex.Message);
        }
    }
}