using System;
using System.IO;

namespace KineGestion.Web.Tests
{
    public class HomeViewTemplateTests
    {
        [Fact]
        public void IndexView_ShouldKeepAccessibleOperationalAlertOriginBadgeMarkup()
        {
            var content = File.ReadAllText(GetHomeViewPath());

            // Verifica que el badge de alerta operativa esté presente
            Assert.Contains("kg-alert-origin-badge", content, StringComparison.Ordinal);

            // Verifica el atributo tabindex (está en el menú de sede y en el badge)
            Assert.Contains("tabindex=\"0\"", content, StringComparison.Ordinal);

            // Verifica que el título esté presente (el contenido varía si es sistema o manual,
            // pero el atributo title siempre está)
            Assert.Contains("title=", content, StringComparison.Ordinal);

            // Verifica que el aria-label esté presente
            Assert.Contains("aria-label=", content, StringComparison.Ordinal);

            // Verifica las etiquetas de disparo (aparecen en el HTML renderizado)
            Assert.Contains("Disparo automático por sistema", content, StringComparison.Ordinal);
            Assert.Contains("Disparo manual por usuario", content, StringComparison.Ordinal);
        }

        [Fact]
        public void IndexView_ShouldRenderSingleCanonicalKpiBlock_AndDynamicVersion()
        {
            var content = File.ReadAllText(GetHomeViewPath());

            Assert.Equal(1, content.Split("Cumplimiento hoy").Length - 1);
            Assert.Equal(1, content.Split("Tasa de cobranza").Length - 1);
            Assert.Equal(1, content.Split("Tasa de cancelación").Length - 1);
            Assert.DoesNotContain("v1.0 — Sprint 2", content, StringComparison.Ordinal);
            Assert.Contains("GetEntryAssembly", content, StringComparison.Ordinal);
            Assert.Contains("Actualizado", content, StringComparison.Ordinal);
            Assert.Contains("Por sede", content, StringComparison.Ordinal);
            Assert.Contains("Global", content, StringComparison.Ordinal);
        }

        [Fact]
        public void IndexView_ShouldRenderAgendaPanelAndChartMarkup()
        {
            var content = File.ReadAllText(GetHomeViewPath());

            Assert.Contains("Agenda de hoy", content, StringComparison.Ordinal);
            Assert.Contains("kg-agenda-list", content, StringComparison.Ordinal);
            Assert.Contains("homeStatusChart", content, StringComparison.Ordinal);
            Assert.Contains("homeEvolutionChart", content, StringComparison.Ordinal);
            Assert.Contains("homeCancellationChart", content, StringComparison.Ordinal);
            Assert.Contains("chart.umd.min.js", content, StringComparison.Ordinal);
            Assert.True(content.Split("kg-chart-data").Length - 1 >= 3);
        }

        private static string GetHomeViewPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "KineGestion.Web",
                "Views",
                "Home",
                "Index.cshtml"));
        }
    }
}