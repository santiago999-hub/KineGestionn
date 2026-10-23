using System;
using System.ComponentModel.DataAnnotations;

namespace KineGestion.Web.Models.ViewModels
{
    /// <summary>
    /// DTO para reporte de conciliación de obras sociales.
    /// Muestra sesiones pendientes, porcentajes de cobertura y montos desglosados.
    /// Modelo flexible: si cambia el % de cobertura de una OS, solo se actualiza el JSON de configuración,
    /// no es necesario tocar el código ni hacer migración de base de datos.
    /// </summary>
    public class OsConciliationDto
    {
        /// <nombre de la obra social (OSDE, PAMI, IOMA, Swiss, etc.)>
        public string ObraSocial { get; set; } = string.Empty;

        /// <cantidad de sesiones con PaymentStatus=Pendiente para esta OS>
        public int SesionesPendientes { get; set; }

        /// <porcentaje de cobertura aplicado por la obra social (ej. 80.00 = cubre el 80%)>
        /// <value>Los valores típicos son: OSDE=80, PAMI=70, IOMA=60, Swiss=75. Se configuran en pricing.json.</value>
        [Range(0.01, 100.00, ErrorMessage = "El porcentaje de cobertura debe estar entre 0.01 y 100")]
        public decimal PorcentajeCobertura { get; set; }

        /// <precio base estándar por sesión (configurable, ej. $1000.00)>
        /// <remarks>Este es el precio referencia. El sistema multiplica por el porcentaje para obtener el valor efectivo.</remarks>
        [Range(0.01, 10000.00, ErrorMessage = "El precio base debe estar entre $0.01 y $10000.00")]
        public decimal PrecioBase { get; set; } = 1000.00m;

        /// <monto total SIN descuento: SesionesPendientes x PrecioBase>
        public decimal TotalSinDescuento { get; set; }

        /// <monto total CON descuento (cobertura aplicada): SesionesPendientes x PrecioBase x PorcentajeCobertura/100>
        public decimal TotalConDescuento { get; set; }

        /// <faldo por cobrar al paciente: TotalSinDescuento - TotalConDescuento>
        public decimal FaldoPorCobrar { get; set; }
    }
}