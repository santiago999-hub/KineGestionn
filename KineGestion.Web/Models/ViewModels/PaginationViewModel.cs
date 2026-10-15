using System.Collections.Generic;

namespace KineGestion.Web.Models.ViewModels
{
    public class PaginationViewModel
    {
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string Action { get; set; } = "Index";
        public string? Label { get; set; }
        public string PreviousText { get; set; } = "Anterior";
        public string NextText { get; set; } = "Siguiente";
        public IDictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();
        public string WrapperClass { get; set; } = "kg-pagination";
        public string LabelClass { get; set; } = "kg-pagination-label";
        public string ActionsClass { get; set; } = "kg-pagination-actions";

        public string LabelText => string.IsNullOrEmpty(Label) ? $"Página {Page} de {TotalPages}" : Label;

        public IDictionary<string, string> PreviousRouteValues => BuildRouteValues(Page - 1);

        public IDictionary<string, string> NextRouteValues => BuildRouteValues(Page + 1);

        private IDictionary<string, string> BuildRouteValues(int page)
        {
            var values = new Dictionary<string, string>(RouteValues);
            values["page"] = page.ToString();
            return values;
        }
    }
}
