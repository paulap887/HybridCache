using System.Net;
using System.Net.Http.Json;
using HybridCache.Api.Models;
using HybridCache.Api.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HybridCache.Api.Tests;

public class ProductEndpointsTests : IDisposable
{
    private readonly CountingProductRepository _repo = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProductEndpointsTests()
    {
        // A fresh host per test, so each test starts with an empty cache.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IProductRepository>(_repo))));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAll_SecondCall_IsServedFromCache()
    {
        var first = await _client.GetFromJsonAsync<List<Product>>("/products");
        var second = await _client.GetFromJsonAsync<List<Product>>("/products");

        Assert.Equal(1, _repo.GetAllCalls);
        Assert.Equal(first!.Select(p => p.Id), second!.Select(p => p.Id));
    }

    [Fact]
    public async Task GetById_SecondCall_IsServedFromCache()
    {
        await _client.GetAsync("/products/1");
        await _client.GetAsync("/products/1");

        Assert.Equal(1, _repo.GetByIdCalls);
    }

    [Fact]
    public async Task Create_InvalidatesList()
    {
        await _client.GetAsync("/products");

        var response = await _client.PostAsJsonAsync("/products", new Product { Name = "Monitor", Category = "Electronics", Price = 399.99m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Product>();

        var list = await _client.GetFromJsonAsync<List<Product>>("/products");
        Assert.Contains(list!, p => p.Id == created!.Id);
        Assert.Equal(2, _repo.GetAllCalls);
    }

    [Fact]
    public async Task Create_ClearsCachedNotFoundForNewId()
    {
        // The seed data has ids 1-3, so the next created product gets id 5.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/products/5")).StatusCode);

        await _client.PostAsJsonAsync("/products", new Product { Name = "Monitor", Category = "Electronics", Price = 399.99m });

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/products/5")).StatusCode);
    }

    [Fact]
    public async Task Update_InvalidatesItemAndList()
    {
        await _client.GetAsync("/products");
        await _client.GetAsync("/products/1");

        await _client.PutAsJsonAsync("/products/1", new Product { Name = "Gaming Laptop", Category = "Electronics", Price = 1499.99m });

        var item = await _client.GetFromJsonAsync<Product>("/products/1");
        var list = await _client.GetFromJsonAsync<List<Product>>("/products");
        Assert.Equal("Gaming Laptop", item!.Name);
        Assert.Equal("Gaming Laptop", list!.Single(p => p.Id == 1).Name);
    }

    [Fact]
    public async Task Delete_InvalidatesItemAndList()
    {
        await _client.GetAsync("/products");
        await _client.GetAsync("/products/2");

        var response = await _client.DeleteAsync("/products/2");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/products/2")).StatusCode);
        var list = await _client.GetFromJsonAsync<List<Product>>("/products");
        Assert.DoesNotContain(list!, p => p.Id == 2);
    }

    [Fact]
    public async Task UnknownId_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/products/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync("/products/999", new Product())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/products/999")).StatusCode);
    }
}
