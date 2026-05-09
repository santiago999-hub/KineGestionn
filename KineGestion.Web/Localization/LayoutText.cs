using System.Globalization;
using System.Resources;

namespace KineGestion.Web.Localization
{
    public static class LayoutText
    {
        private static readonly ResourceManager Resources = new(
            "KineGestion.Web.Resources.SharedLayout",
            typeof(LayoutText).Assembly);

        public static string Get(string key, string fallback)
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }
    }
}