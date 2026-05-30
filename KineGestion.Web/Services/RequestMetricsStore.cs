using System.Collections.Concurrent;
using System.Globalization;

namespace KineGestion.Web.Services
{
    public sealed class RequestMetricsStore
    {
        private long _totalRequests;
        private long _successfulRequests;
        private long _clientErrorRequests;
        private long _serverErrorRequests;
        private long _slowRequests;
        private long _totalElapsedMs;
        private long _maxElapsedMs;

        private readonly ConcurrentDictionary<string, PathMetrics> _pathMetrics = new(StringComparer.OrdinalIgnoreCase);

        public void RecordRequest(string path, string method, int statusCode, long elapsedMs)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Add(ref _totalElapsedMs, elapsedMs);
            UpdateMaxElapsed(elapsedMs);

            if (statusCode >= 500)
                Interlocked.Increment(ref _serverErrorRequests);
            else if (statusCode >= 400)
                Interlocked.Increment(ref _clientErrorRequests);
            else
                Interlocked.Increment(ref _successfulRequests);

            if (elapsedMs >= 1000)
                Interlocked.Increment(ref _slowRequests);

            var key = $"{method.ToUpperInvariant()} {path}";
            var metrics = _pathMetrics.GetOrAdd(key, static _ => new PathMetrics());
            metrics.Record(elapsedMs, statusCode);
        }

        public RequestMetricsSnapshot GetSnapshot()
        {
            var totalRequests = Interlocked.Read(ref _totalRequests);
            var successRequests = Interlocked.Read(ref _successfulRequests);
            var clientErrors = Interlocked.Read(ref _clientErrorRequests);
            var serverErrors = Interlocked.Read(ref _serverErrorRequests);
            var slowRequests = Interlocked.Read(ref _slowRequests);
            var totalElapsed = Interlocked.Read(ref _totalElapsedMs);
            var maxElapsed = Interlocked.Read(ref _maxElapsedMs);
            var averageElapsed = totalRequests == 0 ? 0 : (double)totalElapsed / totalRequests;

            var topPaths = _pathMetrics
                .OrderByDescending(pair => pair.Value.Count)
                .Take(5)
                .Select(pair => pair.Value.ToSnapshot(pair.Key))
                .ToArray();

            return new RequestMetricsSnapshot
            {
                TotalRequests = totalRequests,
                SuccessfulRequests = successRequests,
                ClientErrorRequests = clientErrors,
                ServerErrorRequests = serverErrors,
                SlowRequests = slowRequests,
                AverageDurationMs = averageElapsed,
                MaxDurationMs = maxElapsed,
                TopPaths = topPaths
            };
        }

        private void UpdateMaxElapsed(long elapsedMs)
        {
            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxElapsedMs);
                if (elapsedMs <= currentMax)
                    return;
            }
            while (Interlocked.CompareExchange(ref _maxElapsedMs, elapsedMs, currentMax) != currentMax);
        }

        private sealed class PathMetrics
        {
            private long _count;
            private long _totalElapsedMs;
            private long _maxElapsedMs;
            private long _successCount;
            private long _clientErrorCount;
            private long _serverErrorCount;

            public long Count => Interlocked.Read(ref _count);

            public void Record(long elapsedMs, int statusCode)
            {
                Interlocked.Increment(ref _count);
                Interlocked.Add(ref _totalElapsedMs, elapsedMs);
                UpdateMaxElapsed(elapsedMs);

                if (statusCode >= 500)
                    Interlocked.Increment(ref _serverErrorCount);
                else if (statusCode >= 400)
                    Interlocked.Increment(ref _clientErrorCount);
                else
                    Interlocked.Increment(ref _successCount);
            }

            public RequestPathMetricSnapshot ToSnapshot(string path)
            {
                var count = Interlocked.Read(ref _count);
                var totalElapsed = Interlocked.Read(ref _totalElapsedMs);
                var averageElapsed = count == 0 ? 0 : (double)totalElapsed / count;

                return new RequestPathMetricSnapshot
                {
                    Path = path,
                    Requests = count,
                    SuccessfulRequests = Interlocked.Read(ref _successCount),
                    ClientErrorRequests = Interlocked.Read(ref _clientErrorCount),
                    ServerErrorRequests = Interlocked.Read(ref _serverErrorCount),
                    AverageDurationMs = averageElapsed,
                    MaxDurationMs = Interlocked.Read(ref _maxElapsedMs)
                };
            }

            private void UpdateMaxElapsed(long elapsedMs)
            {
                long currentMax;
                do
                {
                    currentMax = Interlocked.Read(ref _maxElapsedMs);
                    if (elapsedMs <= currentMax)
                        return;
                }
                while (Interlocked.CompareExchange(ref _maxElapsedMs, elapsedMs, currentMax) != currentMax);
            }
        }
    }

    public sealed class RequestMetricsSnapshot
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long ClientErrorRequests { get; set; }
        public long ServerErrorRequests { get; set; }
        public long SlowRequests { get; set; }
        public double AverageDurationMs { get; set; }
        public long MaxDurationMs { get; set; }
        public RequestPathMetricSnapshot[] TopPaths { get; set; } = Array.Empty<RequestPathMetricSnapshot>();
    }

    public sealed class RequestPathMetricSnapshot
    {
        public string Path { get; set; } = string.Empty;
        public long Requests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long ClientErrorRequests { get; set; }
        public long ServerErrorRequests { get; set; }
        public double AverageDurationMs { get; set; }
        public long MaxDurationMs { get; set; }
    }
}