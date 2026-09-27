using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly string _dbName;

    public BulkCategoryUpdateTests(WebApplicationFactory<Program> factory)
    {
        // Capture once per test method. If this were inside the AddDbContext
        // lambda, the API and SeedAsync would see different databases.
        _dbName = "bulk-" + Guid.NewGuid();

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
                    opt.UseInMemoryDatabase(_dbName));
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

        db.Manufacturers.Add(new Manufacturer { Id = 1, Name = "Mazda" });

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

        var medium = result.Single(c => c.Name == "Medium");
        var heavy = result.Single(c => c.Name == "Heavy");
        Assert.Equal(2000m, medium.MaxWeightKg);
        Assert.Equal(2000m, heavy.MinWeightKg);
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
        await SeedAsync();
        var client = _factory.CreateClient();
        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>());

        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_shared_boundary_returns_409_with_bulk_hint()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        // Change Medium's max from 2500 to 2000 without touching Heavy.
        var payload = new UpsertCategoryRequest("Medium", 500m, 2000m, "medium.svg");

        var response = await client.PutAsJsonAsync("/api/categories/2", payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("bulk-edit", body.GetProperty("suggestedAction").GetString());
    }

    [Fact]
    public async Task Renaming_a_category_still_succeeds()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        // Same range, new name — should succeed because the range is unchanged.
        var payload = new UpsertCategoryRequest("Midweight", 500m, 2500m, "medium.svg");

        var response = await client.PutAsJsonAsync("/api/categories/2", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}