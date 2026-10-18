namespace CatalogApi.Models;

public sealed record Product(int Id, string Name, string Category, decimal Price, int Stock);

public sealed record CategorySummary(string Category, int ProductCount, decimal AveragePrice);

public sealed record CatalogSummary(
    int TotalProducts,
    int TotalUnitsInStock,
    decimal InventoryValue,
    IReadOnlyList<CategorySummary> Categories);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
