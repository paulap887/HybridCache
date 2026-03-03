using HybridCache.Api.Models;
using HybridCache.Api.Repositories;
using Microsoft.Extensions.Caching.Hybrid;
using HybridCacheService = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace HybridCache.Api.Services;

public class ProductService(IProductRepository repo, HybridCacheService cache) : IProductService
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            "products:all",
            async ct => await repo.GetAllAsync(ct),
            tags: ["products"],
            cancellationToken: ct
        );

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            $"product:{id}",
            async ct => await repo.GetByIdAsync(id, ct),
            tags: ["products", $"product:{id}"],
            cancellationToken: ct
        );

    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        var created = await repo.CreateAsync(product, ct);
        await cache.RemoveByTagAsync("products", ct); // invalidate all list caches
        return created;
    }

    public async Task<Product?> UpdateAsync(int id, Product product, CancellationToken ct = default)
    {
        var updated = await repo.UpdateAsync(id, product, ct);
        if (updated is not null)
            await cache.RemoveByTagAsync($"product:{id}", ct); // invalidates both the item and any list
        return updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var deleted = await repo.DeleteAsync(id, ct);
        if (deleted)
            await cache.RemoveByTagAsync($"product:{id}", ct);
        return deleted;
    }
}
