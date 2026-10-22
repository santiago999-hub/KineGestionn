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