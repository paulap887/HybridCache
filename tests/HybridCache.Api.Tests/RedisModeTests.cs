using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HybridCache.Api.Tests;

/// <summary>Runs the whole API with Redis as L2. Runs only when REDIS_CONNECTION is set (CI starts a Redis service container).</summary>
public class RedisModeTests
{
    /// <summary>
    /// Regression: Microsoft.Extensions.Caching.StackExchangeRedis 9.0.3 deadlocks with AddHybridCache. The Redis cache's
    /// constructor resolves HybridCache, whose constructor resolves the Redis cache, so the first request never returns.
    /// Fixed by upgrading the Redis package (9.0.20 here).
    /// </summary>
    [SkippableFact]
    public async Task Api_WithRedisL2_ServesRequests()
    {
        var connection = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(connection), "Set REDIS_CONNECTION to run against Redis.");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Redis", connection));
        using var client = factory.CreateClient();

        var request = client.GetAsync("/products");
        var completed = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(completed == request, "GET /products did not complete within 10s (HybridCache + Redis deadlock?)");
        (await request).EnsureSuccessStatusCode();
    }
}
