using System;
using System.IO;
using System.Text.RegularExpressions;

namespace KineGestion.Web.Tests
{
    public class BillingViewTemplateTests
    {
        [Fact]
        public void IndexView_ShouldNotNestForms_NorMixLegacyBadges()
        {
            var content = File.ReadAllText(GetBillingViewPath());

            Assert.False(HasNestedForms(content));
            Assert.DoesNotContain("bg-success-subtle", content, StringComparison.Ordinal);
            Assert.DoesNotContain("bg-warning-subtle", content, StringComparison.Ordinal);
            Assert.Contains("form=\"billingBatchForm\"", content, StringComparison.Ordinal);
        }

        [Fact]
        public void IndexView_ShouldKeepSingleExportEntryPoint_AndAccessibleChartFallbacks()
        {
            var content = File.ReadAllText(GetBillingViewPath());

            Assert.Equal(1, content.Split("ExportCsv").Length - 1);
            Assert.True(content.Split("kg-chart-data").Length - 1 >= 3);
            Assert.Contains("<details", content, StringComparison.Ordinal);
            Assert.Contains("Pendientes", content, StringComparison.Ordinal);
            Assert.Contains("Sin cobrar", content, StringComparison.Ordinal);
        }

        private static bool HasNestedForms(string content)
        {
            var depth = 0;
            foreach (Match match in Regex.Matches(content, "</?form\\b"))
            {
                if (string.Equals(match.Value, "<form", StringComparison.Ordinal))
                {
                    depth++;
                    if (depth > 1) return true;
                }
                else
                {
                    depth--;
                }
            }

            return false;
        }

        private static string GetBillingViewPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "KineGestion.Web",
                "Views",
                "Billing",
                "Index.cshtml"));
        }
    }
}
