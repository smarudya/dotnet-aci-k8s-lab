using System.Text.Json;
using CatalogApi.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace CatalogApi.Services;

/// <summary>
/// Read-only catalog backed by a static seed list. The full list is cached in
/// <see cref="IDistributedCache"/> (in-memory by default, Redis when configured).
/// Cache failures never fail a request: the service falls back to the seed data.
/// </summary>
public sealed class CatalogService : ICatalogService
{
    public const string CacheKey = "products:all";
    public const int MaxPageSize = 100;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly IReadOnlyList<Product> Seed = new List<Product>
    {
        new(1, "Mechanical Keyboard", "peripherals", 89.99m, 40),
        new(2, "Wireless Mouse", "peripherals", 29.50m, 120),
        new(3, "27-inch Monitor", "displays", 249.00m, 15),
        new(4, "USB-C Dock", "accessories", 119.00m, 25),
        new(5, "Laptop Stand", "accessories", 39.90m, 60),
        new(6, "Webcam 1080p", "peripherals", 59.00m, 0),
        new(7, "Ultrawide Monitor", "displays", 549.00m, 8),
        new(8, "Cable Organizer Kit", "accessories", 12.75m, 200),
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(IDistributedCache cache, ILogger<CatalogService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<Product>> SearchAsync(
        string? category, decimal? maxPrice, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        IEnumerable<Product> query = await LoadAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));

        if (maxPrice is not null)
            query = query.Where(p => p.Price <= maxPrice.Value);

        var filtered = query.OrderBy(p => p.Id).ToList();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<Product>(items, page, pageSize, filtered.Count);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        => (await LoadAllAsync(ct)).FirstOrDefault(p => p.Id == id);

    public async Task<CatalogSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);

        var categories = all
            .GroupBy(p => p.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategorySummary(g.Key, g.Count(), Math.Round(g.Average(p => p.Price), 2)))
            .OrderBy(c => c.Category, StringComparer.Ordinal)
            .ToList();

        return new CatalogSummary(
            all.Count,
            all.Sum(p => p.Stock),
            all.Sum(p => p.Price * p.Stock),
            categories);
    }

    private async Task<IReadOnlyList<Product>> LoadAllAsync(CancellationToken ct)
    {
        try
        {
            var cached = await _cache.GetStringAsync(CacheKey, ct);
            if (cached is not null)
            {
                var products = JsonSerializer.Deserialize<List<Product>>(cached);
                if (products is not null) return products;
            }

            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl };
            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(Seed), options, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache unavailable; serving catalog from seed data.");
        }

        return Seed;
    }
}
