using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CatalogApi.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    [InlineData("/api/products")]
    [InlineData("/api/products/1")]
    [InlineData("/api/summary")]
    [InlineData("/api/info")]
    public async Task Endpoint_ReturnsOk(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownProduct_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/products/12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
