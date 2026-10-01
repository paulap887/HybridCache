using HybridCache.Api.Models;
using HybridCache.Api.Repositories;

namespace HybridCache.Api.Tests;

/// <summary>Wraps the real repository and counts reads, so tests can tell a cache hit from a factory call.</summary>
public class CountingProductRepository : IProductRepository
{
    private readonly InMemoryProductRepository _inner = new();
    private int _getAllCalls;
    private int _getByIdCalls;

    public int GetAllCalls => Volatile.Read(ref _getAllCalls);
    public int GetByIdCalls => Volatile.Read(ref _getByIdCalls);

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _getAllCalls);
        return _inner.GetAllAsync(ct);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _getByIdCalls);
        return _inner.GetByIdAsync(id, ct);
    }

    public Task<Product> CreateAsync(Product product, CancellationToken ct = default) => _inner.CreateAsync(product, ct);
    public Task<Product?> UpdateAsync(int id, Product product, CancellationToken ct = default) => _inner.UpdateAsync(id, product, ct);
    public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => _inner.DeleteAsync(id, ct);
}
