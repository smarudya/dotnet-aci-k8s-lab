using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CatalogApi.Health;

/// <summary>Readiness check: verifies the configured cache (memory or Redis) can be written and read.</summary>
public sealed class CacheHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public CacheHealthCheck(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            const string key = "health:probe";
            await _cache.SetStringAsync(
                key, "ok",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);

            return await _cache.GetStringAsync(key, cancellationToken) == "ok"
                ? HealthCheckResult.Healthy("cache reachable")
                : HealthCheckResult.Unhealthy("cache read mismatch");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("cache unreachable", ex);
        }
    }
}
