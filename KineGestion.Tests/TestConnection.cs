using System;

namespace KineGestion.Tests
{
    /// <summary>
    /// Cadena de conexión para tests de integración. En local cae a la instancia
    /// SQL Express; en CI (contenedor SQL Server) se sobreescribe vía variable
    /// de entorno KINEGESTION_TEST_CONNECTION usando el placeholder {DatabaseName}.
    /// </summary>
    internal static class TestConnection
    {
        public static string For(string databaseName)
        {
            var template = Environment.GetEnvironmentVariable("KINEGESTION_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(template))
                template = "Server=localhost\\SQLEXPRESS;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

            return template.Replace("{DatabaseName}", databaseName);
        }
    }
}