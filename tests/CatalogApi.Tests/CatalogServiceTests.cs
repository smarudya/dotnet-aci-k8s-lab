using CatalogApi.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CatalogApi.Tests;

public class CatalogServiceTests
{
    private static CatalogService CreateService(IDistributedCache? cache = null) =>
        new(cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<CatalogService>.Instance);

    [Fact]
    public async Task Search_NoFilters_ReturnsAllOrderedById()
    {
        var result = await CreateService().SearchAsync(null, null, 1, 50);

        Assert.Equal(8, result.TotalCount);
        Assert.Equal(result.Items.OrderBy(p => p.Id).Select(p => p.Id), result.Items.Select(p => p.Id));
    }

    [Theory]
    [InlineData("displays", 2)]
    [InlineData("DISPLAYS", 2)]
    [InlineData(" peripherals ", 3)]
    [InlineData("unknown", 0)]
    public async Task Search_FiltersByCategory_CaseInsensitive(string category, int expected)
    {
        var result = await CreateService().SearchAsync(category, null, 1, 50);

        Assert.Equal(expected, result.TotalCount);
    }

    [Fact]
    public async Task Search_MaxPrice_ExcludesMoreExpensiveProducts()
    {
        var result = await CreateService().SearchAsync(null, 40m, 1, 50);

        Assert.All(result.Items, p => Assert.True(p.Price <= 40m));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Search_Paging_ReturnsRequestedSlice()
    {
        var page2 = await CreateService().SearchAsync(null, null, 2, 3);

        Assert.Equal(new[] { 4, 5, 6 }, page2.Items.Select(p => p.Id));
        Assert.Equal(8, page2.TotalCount);
    }

    [Fact]
    public async Task Search_InvalidPaging_IsClamped()
    {
        var result = await CreateService().SearchAsync(null, null, -5, 10_000);

        Assert.Equal(1, result.Page);
        Assert.Equal(CatalogService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task GetById_ExistingAndMissing()
    {
        var service = CreateService();

        Assert.Equal("Wireless Mouse", (await service.GetByIdAsync(2))?.Name);
        Assert.Null(await service.GetByIdAsync(999));
    }

    [Fact]
    public async Task Summary_AggregatesInventoryAndCategories()
    {
        var summary = await CreateService().GetSummaryAsync();

        Assert.Equal(8, summary.TotalProducts);
        Assert.Equal(468, summary.TotalUnitsInStock);
        Assert.Equal(3, summary.Categories.Count);
        Assert.Equal(new[] { "accessories", "displays", "peripherals" },
            summary.Categories.Select(c => c.Category));
        Assert.Equal(399.00m, summary.Categories.Single(c => c.Category == "displays").AveragePrice);
    }

    [Fact]
    public async Task Search_PopulatesCacheOnFirstCall()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = CreateService(cache);

        Assert.Null(await cache.GetStringAsync(CatalogService.CacheKey));
        await service.SearchAsync(null, null, 1, 10);

        Assert.NotNull(await cache.GetStringAsync(CatalogService.CacheKey));
    }

    [Fact]
    public async Task Search_CacheFailure_FallsBackToSeedData()
    {
        var result = await CreateService(new ThrowingCache()).SearchAsync(null, null, 1, 50);

        Assert.Equal(8, result.TotalCount);
    }

    private sealed class ThrowingCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("cache down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("cache down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("cache down");
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
