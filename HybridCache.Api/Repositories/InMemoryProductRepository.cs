using System.Collections.Concurrent;
using HybridCache.Api.Models;

namespace HybridCache.Api.Repositories;

public class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _store;
    private int _nextId = 4;

    public InMemoryProductRepository()
    {
        _store = new ConcurrentDictionary<int, Product>
        {
            [1] = new() { Id = 1, Name = "Laptop", Category = "Electronics", Price = 999.99m },
            [2] = new() { Id = 2, Name = "Desk Chair", Category = "Furniture", Price = 249.99m },
            [3] = new() { Id = 3, Name = "Coffee Mug", Category = "Kitchen", Price = 12.99m },
        };
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct); // simulate DB latency
        return _store.Values.OrderBy(p => p.Id).ToList();
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        return _store.GetValueOrDefault(id);
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        product.Id = Interlocked.Increment(ref _nextId);
        _store[product.Id] = product;
        return product;
    }

    public async Task<Product?> UpdateAsync(int id, Product product, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        if (!_store.ContainsKey(id)) return null;
        product.Id = id;
        _store[id] = product;
        return product;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        return _store.TryRemove(id, out _);
    }
}
