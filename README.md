# HybridCache

A minimal API demonstrating `Microsoft.Extensions.Caching.Hybrid` (HybridCache), the two-level caching abstraction introduced in .NET 9. It shows cache population, tag-based invalidation, and the L1 → L2 → factory lookup chain using a simple Product CRUD API.

## What is HybridCache?

HybridCache sits in front of a backing store and maintains two cache layers:

| Layer | Store | Scope |
|-------|-------|-------|
| **L1** | In-process `IMemoryCache` | Single process / pod |
| **L2** | `IDistributedCache` (Redis, SQL, etc.) | Shared across all instances |

On a cache miss, HybridCache calls the factory function (your repository), stores the result in both layers, and returns it. Subsequent requests hit L1 first — typically under 5 ms — with no network round-trip.
 
## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker (optional, for Redis L2 cache)

### Run with in-memory distributed cache (no Redis)

```bash
cd HybridCache.Api
dotnet run
```

The API starts on `http://localhost:5086`. `AddDistributedMemoryCache()` is used as the L2 store — suitable for single-instance development.

### Run with Redis (L2 distributed cache)

Start Redis via Docker Compose:

```bash
docker compose up -d
```

Then configure `IDistributedCache` to use Redis (e.g. `AddStackExchangeRedisCache`) and run:

```bash
cd HybridCache.Api
dotnet run
```

## API Endpoints

Base URL: `http://localhost:5086`

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/products` | List all products |
| `GET` | `/products/{id}` | Get product by ID |
| `POST` | `/products` | Create a product |
| `PUT` | `/products/{id}` | Update a product |
| `DELETE` | `/products/{id}` | Delete a product |

Interactive docs are available at `http://localhost:5086/scalar/v1` (Scalar UI).

## Cache Behaviour

### Cache keys and tags

| Operation | Cache key | Tags applied |
|-----------|-----------|--------------|
| Get all | `products:all` | `products` |
| Get by ID | `product:{id}` | `products`, `product:{id}` |

### Invalidation strategy

Write operations use `RemoveByTagAsync` to bust the relevant entries:

- **Create** — removes everything tagged `products` (list caches).
- **Update** — removes everything tagged `product:{id}` (item + any list that cached it).
- **Delete** — removes everything tagged `product:{id}`.

### Cache expiry defaults

| Setting | Value |
|---------|-------|
| L2 (distributed) expiration | 5 minutes |
| L1 (local memory) expiration | 1 minute |

## Running the Test Suite

The shell script exercises the full cache lifecycle:

```bash
chmod +x HybridCache.Api/tests/test-commands.sh
./HybridCache.Api/tests/test-commands.sh
```

Tests covered:

1. **Cache MISS** — first request hits the factory (~80 ms with simulated DB latency)
2. **L1 Cache HIT** — second request served from in-process memory (<5 ms)
3. **Per-item miss then hit** — validates individual product caching
4. **Invalidation on CREATE** — list cache is busted; next GET is slow again
5. **Invalidation on UPDATE** — item cache is busted; re-fetched then re-cached
6. **DELETE + 404** — item removed from cache and store

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.Caching.Hybrid` | 9.3.0 | HybridCache implementation |
| `Microsoft.AspNetCore.OpenApi` | 9.0.3 | OpenAPI document generation |
| `Scalar.AspNetCore` | 2.12.48 | Interactive API docs UI |
