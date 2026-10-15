namespace KineGestion.Web.Models.ViewModels
{
    public class EmptyStateViewModel
    {
        public string? IconClass { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string WrapperClass { get; set; } = "kg-empty";
        public string? ActionName { get; set; }
        public string? ActionText { get; set; }
        public string? ActionIcon { get; set; }
    }
}
