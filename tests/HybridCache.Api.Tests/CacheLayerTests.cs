using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HybridCacheService = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace HybridCache.Api.Tests;

/// <summary>
/// Shows the L1 -> L2 -> factory chain: two HybridCache instances stand in for two app instances (pods),
/// each with its own L1, sharing one L2.
/// </summary>
public class CacheLayerTests
{
    private static HybridCacheService CreateInstance(IDistributedCache sharedL2)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sharedL2);
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCacheService>();
    }

    private static MemoryDistributedCache NewMemoryDistributedCache() =>
        new(Options.Create(new MemoryDistributedCacheOptions()));

    private static async Task AssertSecondInstanceReadsFromL2(IDistributedCache sharedL2)
    {
        var instanceA = CreateInstance(sharedL2);
        var instanceB = CreateInstance(sharedL2);
        var key = $"test:{Guid.NewGuid()}";
        var factoryCalls = 0;

        ValueTask<string> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            return ValueTask.FromResult("value");
        }

        Assert.Equal("value", await instanceA.GetOrCreateAsync(key, Factory)); // miss: factory, then written to L1(A) and L2
        Assert.Equal("value", await instanceA.GetOrCreateAsync(key, Factory)); // L1(A) hit

        // HybridCache can return the value before its L2 write completes (it is a real network call with Redis),
        // so wait for the entry to land in L2 before asking the second instance.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (await sharedL2.GetAsync(key) is null && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal("value", await instanceB.GetOrCreateAsync(key, Factory)); // L1(B) empty, L2 hit

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public Task SecondInstance_ReadsFromL2_WithoutCallingFactory() =>
        AssertSecondInstanceReadsFromL2(new PassThroughDistributedCache(NewMemoryDistributedCache()));

    /// <summary>
    /// Gotcha: HybridCache detects AddDistributedMemoryCache()'s MemoryDistributedCache and skips it, because an
    /// in-process "distributed" cache adds nothing over L1. Registering it gives you an L1-only cache.
    /// </summary>
    [Fact]
    public async Task MemoryDistributedCache_IsNotUsedAsL2()
    {
        var memoryL2 = NewMemoryDistributedCache();
        var cache = CreateInstance(memoryL2);

        await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult("value"));

        Assert.Null(await memoryL2.GetAsync("key"));
    }

    /// <summary>Runs only when REDIS_CONNECTION is set (CI starts a Redis service container).</summary>
    [SkippableFact]
    public async Task SecondInstance_ReadsFromRedisL2_WithoutCallingFactory()
    {
        var connection = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(connection), "Set REDIS_CONNECTION to run against Redis.");

        using var redis = new RedisCache(Options.Create(new RedisCacheOptions { Configuration = connection, InstanceName = "hybridcache-tests:" }));
        await AssertSecondInstanceReadsFromL2(redis);
    }
}

/// <summary>
/// A plain IDistributedCache wrapper. Because it is not MemoryDistributedCache, HybridCache treats it as a real L2,
/// which lets the L1 -> L2 -> factory chain be tested without Redis.
/// </summary>
public class PassThroughDistributedCache(IDistributedCache inner) : IDistributedCache
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
