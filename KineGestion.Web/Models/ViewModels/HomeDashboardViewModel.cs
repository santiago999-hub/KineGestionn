namespace KineGestion.Web.Models.ViewModels
{
    public class HomeDashboardViewModel
    {
        public int PacientesActivosCount { get; set; }
        public int ProfesionalesActivosCount { get; set; }
        public int TratamientosCount { get; set; }
        public int SesionesCount { get; set; }
        public int SesionesHoyCount { get; set; }
        public int SesionesCompletadasHoyCount { get; set; }
        public int SesionesPendientesPagoCount { get; set; }
        public int SesionesPendientesConfirmacionCount { get; set; }
    }
}
