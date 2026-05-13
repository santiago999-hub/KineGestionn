using System.IO;

namespace KineGestion.Web.Tests
{
    public class AuditViewTemplateTests
    {
        [Fact]
        public void IndexView_ShouldUseAuditTextHelper_ForStaticTexts()
        {
            var content = File.ReadAllText(GetAuditViewPath());

            Assert.Contains("@using KineGestion.Web.Localization", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.HeaderTitle\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Table.Date\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.PageTemplate\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Placeholder.RecordId\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Placeholder.User\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Button.Search\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Button.ExportCsv\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Button.ExportExcel\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.View.Button.Clear\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"@AuditText.Get(\"Audit.View.Button.Search\"", content, StringComparison.Ordinal);
            Assert.Contains("AuditText.Get(\"Audit.Export.DateFormat\"", content, StringComparison.Ordinal);
        }

        [Fact]
        public void IndexView_ShouldUseCentralizedLabelHelpers_InGridRows()
        {
            var content = File.ReadAllText(GetAuditViewPath());

            Assert.Contains("AuditIndexViewModel.GetEntityLabel(item.EntityName)", content, StringComparison.Ordinal);
            Assert.Contains("AuditIndexViewModel.GetActionLabel(item.Action)", content, StringComparison.Ordinal);
        }

        private static string GetAuditViewPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "KineGestion.Web",
                "Views",
                "Audit",
                "Index.cshtml"));
        }
    }
}
