using CatalogApi.Health;
using CatalogApi.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Redis is optional: set ConnectionStrings__Redis (e.g. "redis:6379,abortConnect=false").
var redis = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redis))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redis;
        o.InstanceName = "catalog:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<ICatalogService, CatalogService>();
builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache", tags: new[] { "ready" });

var app = builder.Build();

// Liveness: process is up (no dependency checks). Readiness: dependencies usable.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.MapGet("/", () => Results.Ok(new { service = "catalog-api", docs = "see README", endpoints = new[]
{
    "/api/products", "/api/products/{id}", "/api/summary", "/api/info", "/health", "/ready"
}}));

app.MapGet("/api/products", async (
    string? category, decimal? maxPrice, int? page, int? pageSize,
    ICatalogService catalog, CancellationToken ct) =>
    Results.Ok(await catalog.SearchAsync(category, maxPrice, page ?? 1, pageSize ?? 20, ct)));

app.MapGet("/api/products/{id:int}", async (int id, ICatalogService catalog, CancellationToken ct) =>
    await catalog.GetByIdAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());

app.MapGet("/api/summary", async (ICatalogService catalog, CancellationToken ct) =>
    Results.Ok(await catalog.GetSummaryAsync(ct)));

// Shows which replica served the request (useful to demo Service load balancing).
app.MapGet("/api/info", (IHostEnvironment env) => Results.Ok(new
{
    host = Environment.MachineName,
    environment = env.EnvironmentName,
    cache = string.IsNullOrWhiteSpace(redis) ? "memory" : "redis",
}));

app.Run();

public partial class Program { }
