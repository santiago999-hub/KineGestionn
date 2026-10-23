using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using KineGestion.Web.Models.ViewModels;

namespace KineGestion.Web.Services
{
    public class OsConciliationService
    {
        private readonly AppDbContext _context;

        public OsConciliationService(AppDbContext context)
        {
            _context = context;
        }

        public List<OsConciliationDto> GetReporteAsync(int daysAtrás = 30)
        {
            var fechaInicio = DateTime.UtcNow.Date.AddDays(-daysAtrás);

            var pricing = LeerConfigPrecios();

            var query = _context.Sessions
                .Where(s => s.PaymentStatus == 0 && s.FechaHora >= fechaInicio)
                .GroupBy(s => s.Patient.ObraSocial)
                .Select(g => new
                {
                    ObraSocial = g.Key,
                    SesionesPendientes = g.Count()
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.ObraSocial))
                .ToList();

            var resultado = new List<OsConciliationDto>();

            foreach (var item in query)
            {
                // Obtener porcentaje de esta OS (o DEFAULT)
                // Usamos ! para indicar que no es nulo después del check
                decimal porcentaje = pricing.ContainsKey(item.ObraSocial)
                    ? pricing[item.ObraSocial]
                    : pricing["DEFAULT"];

                decimal precioBase = 1000.00m;

                decimal totalSinDescuento = item.SesionesPendientes * precioBase;
                decimal totalConDescuento = Math.Round(item.SesionesPendientes * precioBase * porcentaje / 100, 2);
                decimal faldoPorCobrar = Math.Round(totalSinDescuento - totalConDescuento, 2);

                resultado.Add(new OsConciliationDto
                {
                    ObraSocial = item.ObraSocial,
                    SesionesPendientes = item.SesionesPendientes,
                    PorcentajeCobertura = porcentaje,
                    PrecioBase = precioBase,
                    TotalSinDescuento = totalSinDescuento,
                    TotalConDescuento = totalConDescuento,
                    FaldoPorCobrar = faldoPorCobrar
                });
            }

            var totalDto = new OsConciliationDto
            {
                ObraSocial = "TOTAL",
                SesionesPendientes = resultado.Sum(x => x.SesionesPendientes),
                PorcentajeCobertura = 0,
                PrecioBase = 1000.00m,
                TotalSinDescuento = resultado.Sum(x => x.TotalSinDescuento),
                TotalConDescuento = resultado.Sum(x => x.TotalConDescuento),
                FaldoPorCobrar = resultado.Sum(x => x.FaldoPorCobrar)
            };
            resultado.Add(totalDto);

            return resultado;
        }

        private Dictionary<string, decimal> LeerConfigPrecios()
        {
            var dict = new Dictionary<string, decimal>();
            var pricingPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/config/pricing.json");

            if (File.Exists(pricingPath))
            {
                var json = File.ReadAllText(pricingPath);
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);

                if (data != null)
                {
                    foreach (var prop in data)
                    {
                        if (prop.Value is not null && decimal.TryParse(prop.Value.ToString(), out var val))
                        {
                            dict[prop.Key] = val;
                        }
                    }
                }
            }

            if (!dict.ContainsKey("DEFAULT"))
                dict["DEFAULT"] = 50.00m;

            return dict;
        }
    }
}