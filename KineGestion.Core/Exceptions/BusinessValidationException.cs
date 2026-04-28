using System;

namespace KineGestion.Core.Exceptions
{
    public class BusinessValidationException : InvalidOperationException
    {
        public string? PropertyName { get; }

        public BusinessValidationException(string message, string? propertyName = null)
            : base(message)
        {
            PropertyName = propertyName;
        }
    }
}