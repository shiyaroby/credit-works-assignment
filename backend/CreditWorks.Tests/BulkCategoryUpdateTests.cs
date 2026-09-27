using System.Net;
using System.Net.Http.Json;
using CreditWorks.Api.Contracts;
using CreditWorks.Core.Models;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditWorks.Tests;

public class BulkCategoryUpdateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BulkCategoryUpdateTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false) ||
                        (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false))
                    .ToList();

                foreach (var d in toRemove) services.Remove(d);

                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseInMemoryDatabase("bulk-" + Guid.NewGuid()));
            });
        });
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.VehicleCategories.AnyAsync()) return;

        db.VehicleCategories.AddRange(
            new VehicleCategory { Id = 1, Name = "Light", MinWeightKg = 0m, MaxWeightKg = 500m, IconName = "light.svg" },
            new VehicleCategory { Id = 2, Name = "Medium", MinWeightKg = 500m, MaxWeightKg = 2500m, IconName = "medium.svg" },
            new VehicleCategory { Id = 3, Name = "Heavy", MinWeightKg = 2500m, MaxWeightKg = null, IconName = "heavy.svg" });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Bulk_replace_can_move_a_shared_boundary_in_one_call()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>
        {
            new("Light",  0m,    500m,  "light.svg"),
            new("Medium", 500m,  2000m, "medium.svg"),
            new("Heavy",  2000m, null,  "heavy.svg")
        });

        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Equal(2000m, result.Single(c => c.Name == "Medium").MaxWeightKg);
        Assert.Equal(2000m, result.Single(c => c.Name == "Heavy").MinWeightKg);
    }

    [Fact]
    public async Task Bulk_replace_rejects_a_set_with_a_gap()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>
        {
            new("Light",  0m,    500m,  "light.svg"),
            new("Medium", 600m,  2000m, "medium.svg"),
            new("Heavy",  2500m, null,  "heavy.svg")
        });

        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_replace_rejects_a_set_with_an_overlap()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>
        {
            new("Light",  0m,    600m,  "light.svg"),
            new("Medium", 500m,  2500m, "medium.svg"),
            new("Heavy",  2500m, null,  "heavy.svg")
        });

        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_replace_rejects_empty_list()
    {
        var client = _factory.CreateClient();
        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>());

        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}