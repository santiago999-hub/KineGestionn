using KineGestion.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace KineGestion.Web.Services
{
    /// <summary>
    /// Implementación de IEncryptionService basada en ASP.NET Data Protection.
    /// Usa un purpose string dedicado para los datos clínicos (independiente de cookies/antiforgery).
    /// El texto encriptado se prefija con un marker para detectar valores ya encriptados y
    /// evitar doble encriptación.
    /// </summary>
    public class DataProtectionEncryptionService : IEncryptionService
    {
        private const string CipherPrefix = "kg1:";
        private readonly IDataProtector _protector;

        public DataProtectionEncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("KineGestion.ClinicalData.v1");
        }

        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            if (plainText.StartsWith(CipherPrefix, StringComparison.Ordinal))
                return plainText;

            var cipher = _protector.Protect(plainText);
            return CipherPrefix + cipher;
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;

            if (!cipherText.StartsWith(CipherPrefix, StringComparison.Ordinal))
                return cipherText;

            try
            {
                return _protector.Unprotect(cipherText[CipherPrefix.Length..]);
            }
            catch
            {
                return cipherText;
            }
        }

        public bool TryDecrypt(string? cipherText, out string? plainText)
        {
            if (string.IsNullOrEmpty(cipherText) || !cipherText.StartsWith(CipherPrefix, StringComparison.Ordinal))
            {
                plainText = cipherText;
                return false;
            }

            try
            {
                plainText = _protector.Unprotect(cipherText[CipherPrefix.Length..]);
                return true;
            }
            catch
            {
                plainText = cipherText;
                return false;
            }
        }
    }
}
