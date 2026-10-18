using CatalogApi.Models;

namespace CatalogApi.Services;

public interface ICatalogService
{
    Task<PagedResult<Product>> SearchAsync(
        string? category, decimal? maxPrice, int page, int pageSize, CancellationToken ct = default);

    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<CatalogSummary> GetSummaryAsync(CancellationToken ct = default);
}
