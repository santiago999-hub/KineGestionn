using System.Collections.Concurrent;

namespace KineGestion.Core.Services
{
    public static class QueryCache
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new(StringComparer.OrdinalIgnoreCase);
        private static int _version;

        /// <summary>
        /// Versión del estado de dominio cacheado. Se incrementa en cada invalidación,
        /// de modo que las claves de caché que dependan de datos mutables puedan incluirla
        /// y queden obsoletas de forma inmediata ante cualquier escritura.
        /// </summary>
        public static int Version => Volatile.Read(ref _version);

        public static async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero)
                return await factory();

            if (TryGetFreshValue(key, out T? cachedValue))
                return cachedValue!;

            var keyLock = KeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync();

            try
            {
                if (TryGetFreshValue(key, out cachedValue))
                    return cachedValue!;

                var value = await factory();
                Entries[key] = new CacheEntry(value!, DateTimeOffset.UtcNow.Add(ttl));
                return value;
            }
            finally
            {
                keyLock.Release();
            }
        }

        public static void InvalidatePrefix(string prefix)
        {
            var keys = Entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var key in keys)
            {
                Entries.TryRemove(key, out _);
                KeyLocks.TryRemove(key, out _);
            }

            Interlocked.Increment(ref _version);
        }

        public static void ClearAll()
        {
            Entries.Clear();
            KeyLocks.Clear();
            Interlocked.Increment(ref _version);
        }

        /// <summary>
        /// Notifica un cambio de datos que no invalida una clave concreta (por ejemplo, un
        /// evento operativo registrado). Incrementa la versión para que las claves que
        /// dependen del estado del dominio queden obsoletas de inmediato.
        /// </summary>
        public static void NotifyDataChanged() => Interlocked.Increment(ref _version);

        private static bool TryGetFreshValue<T>(string key, out T? value)
        {
            if (!Entries.TryGetValue(key, out var existing))
            {
                value = default;
                return false;
            }

            if (existing.TryGetValue(out value))
                return true;

            Entries.TryRemove(key, out _);
            KeyLocks.TryRemove(key, out _);
            value = default;
            return false;
        }

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