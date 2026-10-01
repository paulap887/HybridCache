using HybridCache.Api.Endpoints;
using HybridCache.Api.Repositories;
using HybridCache.Api.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// L2 (distributed) cache: HybridCache uses whichever IDistributedCache is registered.
// Without a Redis connection string it runs L1-only. Note that AddDistributedMemoryCache() would not give you an L2:
// HybridCache deliberately ignores MemoryDistributedCache because it adds nothing over L1.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "hybridcache-demo:";
    });
}

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),          // L2
        LocalCacheExpiration = TimeSpan.FromMinutes(1) // L1
    };
});

builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapProductEndpoints();

app.Run();

public partial class Program;
