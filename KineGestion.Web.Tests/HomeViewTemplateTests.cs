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

            Assert.Contains("kg-alert-origin-badge", content, StringComparison.Ordinal);
            Assert.Contains("tabindex=\"0\"", content, StringComparison.Ordinal);
            Assert.Contains("title=\"@((item.IsSystemTriggered ? \"Origen automático por sistema\" : \"Origen manual disparado por usuario\"))\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"@($\"Origen de alerta:", content, StringComparison.Ordinal);
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