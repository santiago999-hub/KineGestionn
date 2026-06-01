using System.Globalization;

namespace KineGestion.Web.Services
{
    public static class OperationalConfig
    {
        public static int ReadBoundedInt(
            IConfiguration configuration,
            ILogger logger,
            string key,
            int defaultValue,
            int min,
            int max)
        {
            var raw = configuration[key];
            var value = defaultValue;

            if (string.IsNullOrWhiteSpace(raw))
            {
                logger.LogWarning(
                    "Configuración {Key} ausente/vacía. Se aplica default {DefaultValue}.",
                    key,
                    defaultValue);
            }
            else if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                logger.LogWarning(
                    "Configuración {Key} inválida ('{RawValue}'). Se aplica default {DefaultValue}.",
                    key,
                    raw,
                    defaultValue);
                value = defaultValue;
            }

            if (value < min || value > max)
            {
                var clamped = Math.Clamp(value, min, max);
                logger.LogWarning(
                    "Configuración {Key} fuera de rango ({Value}). Se ajusta a {ClampedValue}. Rango permitido: [{Min}, {Max}].",
                    key,
                    value,
                    clamped,
                    min,
                    max);
                value = clamped;
            }

            return value;
        }

        public static List<int> ReadDistinctHourWindows(
            IConfiguration configuration,
            ILogger logger,
            string key,
            IReadOnlyCollection<int> fallback,
            int min,
            int max)
        {
            var raw = configuration[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                logger.LogWarning(
                    "Configuración {Key} ausente/vacía. Se aplican ventanas por defecto: {Fallback}.",
                    key,
                    string.Join(",", fallback));
                return fallback.OrderByDescending(v => v).ToList();
            }

            var values = new List<int>();
            var rejected = new List<string>();

            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    rejected.Add(token);
                    continue;
                }

                if (parsed < min || parsed > max)
                {
                    rejected.Add(token);
                    continue;
                }

                values.Add(parsed);
            }

            var normalized = values
                .Distinct()
                .OrderByDescending(v => v)
                .ToList();

            if (rejected.Count > 0)
            {
                logger.LogWarning(
                    "Configuración {Key}: se descartaron valores inválidos ({RejectedValues}).",
                    key,
                    string.Join(",", rejected));
            }

            if (normalized.Count == 0)
            {
                logger.LogWarning(
                    "Configuración {Key} no dejó ventanas válidas. Se aplican ventanas por defecto: {Fallback}.",
                    key,
                    string.Join(",", fallback));
                return fallback.OrderByDescending(v => v).ToList();
            }

            return normalized;
        }
    }
}