using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace KineGestion.Web.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en la petición {Path}", context.Request.Path);

                if (context.Response.HasStarted)
                    throw;

                context.Response.Clear();
                var friendlyMessage = Uri.EscapeDataString("Se produjo un error inesperado. El equipo ya fue notificado.");
                context.Response.Redirect($"/Home/Error?friendlyMessage={friendlyMessage}");
            }
        }
    }
}
