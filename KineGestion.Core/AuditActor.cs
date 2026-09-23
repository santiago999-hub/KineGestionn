namespace KineGestion.Core
{
    public static class AuditActor
    {
        public const int MaxChangedByLength = 256;

        public static string? Truncate(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxChangedByLength)
                return value;

            return value.Substring(0, MaxChangedByLength);
        }
    }
}