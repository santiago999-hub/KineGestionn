using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using KineGestion.Web.Services;

namespace KineGestion.Web.Middleware
{
    public sealed class RequestMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestMetricsMiddleware> _logger;
        private readonly RequestMetricsStore _metricsStore;
        private readonly int _slowRequestThresholdMs;

        public RequestMetricsMiddleware(
            RequestDelegate next,
            ILogger<RequestMetricsMiddleware> logger,
            IConfiguration configuration,
            RequestMetricsStore metricsStore)
        {
            _next = next;
            _logger = logger;
            _metricsStore = metricsStore;
            _slowRequestThresholdMs = OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Observability:SlowRequestThresholdMs",
                defaultValue: 1000,
                min: 100,
                max: 120000);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Response-Time-Ms"] = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                return Task.CompletedTask;
            });

            await _next(context);

            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;

            _metricsStore.RecordRequest(path, method, statusCode, elapsedMs);

            if (statusCode >= 500)
            {
                _logger.LogError(
                    "Request {Method} {Path} completed with {StatusCode} in {ElapsedMs} ms",
                    method,
                    path,
                    statusCode,
                    elapsedMs);
                return;
            }

            if (statusCode >= 400 || elapsedMs >= _slowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Request {Method} {Path} completed with {StatusCode} in {ElapsedMs} ms",
                    method,
                    path,
                    statusCode,
                    elapsedMs);
                return;
            }

            _logger.LogInformation(
                "Request {Method} {Path} completed with {StatusCode} in {ElapsedMs} ms",
                method,
                path,
                statusCode,
                elapsedMs);
        }
    }
}