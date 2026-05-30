using System.Collections.Concurrent;

namespace KineGestion.Core.Services
{
    public static class QueryCache
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            if (Entries.TryGetValue(key, out var existing) && existing.TryGetValue(out T? cachedValue))
                return cachedValue!;

            var value = await factory();
            Entries[key] = new CacheEntry(value!, DateTimeOffset.UtcNow.Add(ttl));
            return value;
        }

        public static void InvalidatePrefix(string prefix)
        {
            var keys = Entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var key in keys)
                Entries.TryRemove(key, out _);
        }

        public static void ClearAll()
            => Entries.Clear();

        private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt)
        {
            public bool TryGetValue<T>(out T? value)
            {
                if (DateTimeOffset.UtcNow >= ExpiresAt)
                {
                    value = default;
                    return false;
                }

                if (Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                value = default;
                return false;
            }
        }
    }
}