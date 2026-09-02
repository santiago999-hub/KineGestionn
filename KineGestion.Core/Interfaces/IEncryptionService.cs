namespace KineGestion.Core.Interfaces
{
    /// <summary>
    /// Contrato de encriptación de datos sensibles en reposo.
    /// La implementación concreta vive en la capa Web (usa ASP.NET Data Protection),
    /// pero Core solo conoce esta abstracción para no acoplar el dominio a infraestructura.
    /// </summary>
    public interface IEncryptionService
    {
        string? Encrypt(string? plainText);
        string? Decrypt(string? cipherText);
        /// <summary>Indica si el valor ya está encriptado (para evitar doble encriptación).</summary>
        bool TryDecrypt(string? cipherText, out string? plainText);
    }
}
