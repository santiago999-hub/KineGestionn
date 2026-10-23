using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using KineGestion.Web.Services;
using KineGestion.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using KineGestion.Data.Context;

namespace KineGestion.Web.Controllers
{
    [ApiController]
    [Route("api/OsConciliation")]
    public class OsConciliationController : ControllerBase
    {
        private readonly OsConciliationService _service;

        public OsConciliationController(OsConciliationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Obtiene el reporte conciliación de obras sociales.
        /// </summary>
        [HttpGet("Report")]
        public IActionResult Report(int daysAtrás = 30)
        {
            var datos = _service.GetReporteAsync(daysAtrás);
            return Ok(datos);
        }

        /// <summary>
        /// Descarga reporte CSV manual.
        /// </summary>
        [HttpGet("ExportCsv")]
        public IActionResult ExportCsv(int daysAtrás = 30)
        {
            var datos = _service.GetReporteAsync(daysAtrás);

            var csvLines = new List<string>();
            csvLines.Add("Obra Social,Sessions Pending,% Coverage,Base Price,Total Without Discount,Total With Discount,Patient Balance");

            foreach (var item in datos)
            {
                csvLines.Add($"{item.ObraSocial},{item.SesionesPendientes},{item.PorcentajeCobertura},{item.PrecioBase:N2},{item.TotalSinDescuento:N2},{item.TotalConDescuento:N2},{item.FaldoPorCobrar:N2}");
            }

            return Content(string.Join("\n", csvLines), "text/csv");
        }
    }
}