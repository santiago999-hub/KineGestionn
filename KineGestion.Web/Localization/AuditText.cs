using System.Globalization;
using System.Resources;

namespace KineGestion.Web.Localization
{
    public static class AuditText
    {
        private static readonly ResourceManager Resources = new(
            "KineGestion.Web.Resources.AuditLabels",
            typeof(AuditText).Assembly);

        public static string Get(string key, string fallback)
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }
    }
}