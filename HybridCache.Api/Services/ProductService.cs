using HybridCache.Api.Models;
using HybridCache.Api.Repositories;
using HybridCacheService = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace HybridCache.Api.Services;

public class ProductService(IProductRepository repo, HybridCacheService cache) : IProductService
{
    public const string ListKey = "products:all";
    public const string ListTag = "products:list";
    public static string ItemKey(int id) => $"product:{id}";
    public static string ItemTag(int id) => $"product:{id}";

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            ListKey,
            async ct => await repo.GetAllAsync(ct),
            tags: [ListTag],
            cancellationToken: ct
        );

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            ItemKey(id),
            async ct => await repo.GetByIdAsync(id, ct),
            tags: [ItemTag(id)],
            cancellationToken: ct
        );

    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        var created = await repo.CreateAsync(product, ct);
        // The list is now stale, and a GET for this id may have cached a "not found" (null) before it existed.
        await cache.RemoveByTagAsync([ListTag, ItemTag(created.Id)], ct);
        return created;
    }

    public async Task<Product?> UpdateAsync(int id, Product product, CancellationToken ct = default)
    {
        var updated = await repo.UpdateAsync(id, product, ct);
        if (updated is not null)
            await cache.RemoveByTagAsync([ListTag, ItemTag(id)], ct); // the item and the list both contain the old values
        return updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var deleted = await repo.DeleteAsync(id, ct);
        if (deleted)
            await cache.RemoveByTagAsync([ListTag, ItemTag(id)], ct);
        return deleted;
    }
}
