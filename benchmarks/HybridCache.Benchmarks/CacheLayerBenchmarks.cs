using BenchmarkDotNet.Attributes;
using HybridCache.Api.Models;
using HybridCache.Api.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HybridCacheService = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace HybridCache.Benchmarks;

/// <summary>
/// Cost of one GetOrCreateAsync call when served by each layer.
/// L2 is Redis when REDIS_CONNECTION is set, otherwise an in-process IDistributedCache (serialization cost only, no network).
/// The factory uses the sample repository, which simulates 30 ms of database latency.
/// </summary>
[MemoryDiagnoser]
public class CacheLayerBenchmarks
{
    private const string Key = "product:1";

    private static readonly HybridCacheEntryOptions L2Only = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheRead | HybridCacheEntryFlags.DisableLocalCacheWrite
    };

    private static readonly HybridCacheEntryOptions NoCache = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCache | HybridCacheEntryFlags.DisableDistributedCache
    };

    private readonly InMemoryProductRepository _repo = new();
    private ServiceProvider _services = null!;
    private HybridCacheService _cache = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        var redis = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
        if (string.IsNullOrWhiteSpace(redis))
            services.AddSingleton<IDistributedCache>(new PassThroughDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))));
        else
            services.AddSingleton<IDistributedCache>(new RedisCache(Options.Create(new RedisCacheOptions { Configuration = redis, InstanceName = "hybridcache-bench:" })));
        services.AddHybridCache();

        _services = services.BuildServiceProvider();
        _cache = _services.GetRequiredService<HybridCacheService>();

        // Warm both L1 and L2.
        await _cache.GetOrCreateAsync(Key, async ct => await _repo.GetByIdAsync(1, ct));
    }

    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    [Benchmark(Baseline = true, Description = "L1 hit (in-process memory)")]
    public ValueTask<Product?> L1Hit() =>
        _cache.GetOrCreateAsync(Key, async ct => await _repo.GetByIdAsync(1, ct));

    [Benchmark(Description = "L2 hit (distributed cache + deserialize)")]
    public ValueTask<Product?> L2Hit() =>
        _cache.GetOrCreateAsync(Key, async ct => await _repo.GetByIdAsync(1, ct), L2Only);

    [Benchmark(Description = "Miss (factory -> repository, 30 ms simulated DB)")]
    public ValueTask<Product?> Miss() =>
        _cache.GetOrCreateAsync(Key, async ct => await _repo.GetByIdAsync(1, ct), NoCache);
}

/// <summary>Not MemoryDistributedCache, so HybridCache treats it as a real L2 (it skips MemoryDistributedCache).</summary>
internal sealed class PassThroughDistributedCache(IDistributedCache inner) : IDistributedCache
{
    public byte[]? Get(string key) => inner.Get(key);
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => inner.SetAsync(key, value, options, token);
    public void Refresh(string key) => inner.Refresh(key);
    public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
    public void Remove(string key) => inner.Remove(key);
    public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
}
