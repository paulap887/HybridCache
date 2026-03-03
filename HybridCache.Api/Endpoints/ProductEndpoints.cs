using HybridCache.Api.Models;
using HybridCache.Api.Services;

namespace HybridCache.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("/", async (IProductService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, IProductService svc, CancellationToken ct) =>
        {
            var product = await svc.GetByIdAsync(id, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapPost("/", async (Product product, IProductService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(product, ct);
            return Results.Created($"/products/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, Product product, IProductService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateAsync(id, product, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id:int}", async (int id, IProductService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}
