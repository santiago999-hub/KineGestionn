namespace KineGestion.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public string FriendlyMessage { get; set; } = "Ocurrió un error inesperado.";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
