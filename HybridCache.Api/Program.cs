using HybridCache.Api.Endpoints;
using HybridCache.Api.Repositories;
using HybridCache.Api.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;                          // <-- add

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();                    // <-- add

builder.Services.AddDistributedMemoryCache();

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

app.MapOpenApi();                                 // <-- add
app.MapScalarApiReference();

app.MapProductEndpoints();

app.Run();
