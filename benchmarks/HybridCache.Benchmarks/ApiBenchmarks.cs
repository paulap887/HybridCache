using BenchmarkDotNet.Attributes;
using HybridCache.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HybridCache.Benchmarks;

/// <summary>
/// Full request pipeline (routing, endpoint, HybridCache, JSON) via the in-memory TestServer.
/// No real network, so this is the server-side cost of a response; add your network RTT on top.
/// </summary>
[MemoryDiagnoser]
public class ApiBenchmarks
{
    // Any type from the API assembly works as the anchor; "Program" here would resolve to this project's own entry point.
    private WebApplicationFactory<Product> _factory = null!;
    private HttpClient _client = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _factory = new WebApplicationFactory<Product>();
        _client = _factory.CreateClient();
        // Warm the cache.
        (await _client.GetAsync("/products/1")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/products")).EnsureSuccessStatusCode();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Benchmark(Description = "GET /products/1 (cached)")]
    public Task<string> GetProductCached() => _client.GetStringAsync("/products/1");

    [Benchmark(Description = "GET /products (cached)")]
    public Task<string> GetAllCached() => _client.GetStringAsync("/products");
}
