using System.Globalization;
using KineGestion.Web.Localization;

namespace KineGestion.Web.Tests
{
    public class AuditTextLocalizationTests
    {
        [Fact]
        public void Get_ShouldReturnSpanishValue_WhenCurrentCultureIsEs()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUICulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es");
                CultureInfo.CurrentUICulture = new CultureInfo("es");

                var value = AuditText.Get("Audit.View.Table.Date", "Date");

                Assert.Equal("Fecha", value);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }

        [Fact]
        public void Get_ShouldReturnEnglishValue_WhenCurrentCultureIsEn()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUICulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en");
                CultureInfo.CurrentUICulture = new CultureInfo("en");

                var value = AuditText.Get("Audit.View.Table.Date", "Fecha");

                Assert.Equal("Date", value);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }
    }
}
