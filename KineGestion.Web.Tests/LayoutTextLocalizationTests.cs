using System.Globalization;
using KineGestion.Web.Localization;

namespace KineGestion.Web.Tests
{
    public class LayoutTextLocalizationTests
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

                var value = LayoutText.Get("Nav.Patients", "Patients");

                Assert.Equal("Pacientes", value);
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

                var value = LayoutText.Get("Nav.Patients", "Pacientes");

                Assert.Equal("Patients", value);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }
    }
}