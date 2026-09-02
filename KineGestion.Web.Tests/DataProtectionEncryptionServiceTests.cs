using KineGestion.Web.Services;
using Microsoft.AspNetCore.DataProtection;

namespace KineGestion.Web.Tests
{
    public class DataProtectionEncryptionServiceTests
    {
        private readonly DataProtectionEncryptionService _service;

        public DataProtectionEncryptionServiceTests()
        {
            var provider = DataProtectionProvider.Create("KineGestion.Web.Tests");
            _service = new DataProtectionEncryptionService(provider);
        }

        [Fact]
        public void Encrypt_ShouldReturnNull_WhenInputIsNull()
        {
            Assert.Null(_service.Encrypt(null));
        }

        [Fact]
        public void Encrypt_ShouldReturnNull_WhenInputIsEmpty()
        {
            Assert.Null(_service.Encrypt(string.Empty));
        }

        [Fact]
        public void RoundTrip_ShouldReturnOriginalPlainText()
        {
            const string plainText = "Hola, soy el dato clínico del paciente.";

            var encrypted = _service.Encrypt(plainText);
            var decrypted = _service.Decrypt(encrypted);

            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void Encrypt_ShouldNotReturnPlainText()
        {
            const string plainText = "valor-no-debe-verse";

            var encrypted = _service.Encrypt(plainText);

            Assert.NotEqual(plainText, encrypted);
        }

        [Fact]
        public void Encrypt_ShouldPrefixCipherWithMarker()
        {
            var encrypted = _service.Encrypt("dato");

            Assert.StartsWith("kg1:", encrypted, StringComparison.Ordinal);
            Assert.NotEqual("kg1:dato", encrypted);
        }

        [Fact]
        public void Encrypt_ShouldNotDoubleEncrypt_WhenValueAlreadyEncrypted()
        {
            const string plainText = "dato";
            var first = _service.Encrypt(plainText);
            var second = _service.Encrypt(first);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Decrypt_ShouldReturnValueAsIs_WhenNotPrefixedWithMarker()
        {
            const string plainTextWithoutMarker = "sin-prefijo";

            var result = _service.Decrypt(plainTextWithoutMarker);

            Assert.Equal(plainTextWithoutMarker, result);
        }

        [Fact]
        public void Decrypt_ShouldReturnNull_WhenInputIsNull()
        {
            Assert.Null(_service.Decrypt(null));
        }

        [Fact]
        public void TryDecrypt_ShouldReturnFalse_WhenValueIsNotEncrypted()
        {
            var success = _service.TryDecrypt("texto-plano", out var plainText);

            Assert.False(success);
            Assert.Equal("texto-plano", plainText);
        }

        [Fact]
        public void TryDecrypt_ShouldReturnTrueAndPlainText_WhenValueIsEncrypted()
        {
            const string plainText = "dato clínico";
            var encrypted = _service.Encrypt(plainText);

            var success = _service.TryDecrypt(encrypted, out var decrypted);

            Assert.True(success);
            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void TryDecrypt_ShouldReturnFalse_WhenValueCannotBeUnprotected()
        {
            var success = _service.TryDecrypt("kg1:no-es-un-cipher-valido", out var plainText);

            Assert.False(success);
            Assert.Equal("kg1:no-es-un-cipher-valido", plainText);
        }

        [Fact]
        public void RoundTrip_ShouldHandleSpecialCharactersAndAccents()
        {
            const string plainText = "Dato clínico con tildes: áéíóú ñ ü, y símbolos !@#$%^&*()_+";

            var encrypted = _service.Encrypt(plainText);
            var decrypted = _service.Decrypt(encrypted);

            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void Encrypt_ShouldProduceRandomizedCipher_ForSamePlainText()
        {
            // Data Protection incluye salt/noise, por lo que el mismo texto plano
            // no produce el mismo cipher en cada llamada.
            var a = _service.Encrypt("dato");
            var b = _service.Encrypt("dato");

            Assert.NotEqual(a, b);
        }
    }
}
