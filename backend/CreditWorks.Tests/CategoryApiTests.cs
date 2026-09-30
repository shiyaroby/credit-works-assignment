using System.Net;
using System.Net.Http.Json;
using CreditWorks.Api.Contracts;
using Xunit;

namespace CreditWorks.Tests;

public class CategoryApiTests
{
    private static async Task<(ApiFactory factory, HttpClient client)> StartAsync()
    {
        var factory = new ApiFactory();
        await factory.SeedAsync();
        return (factory, factory.CreateClient());
    }

    /// <summary>John Smith is seeded at 2200 kg.</summary>
    private static async Task<string?> CategoryOfJohnAsync(HttpClient client)
    {
        var vehicles = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles");
        return vehicles!.Single(v => v.OwnerName == "John Smith").CategoryName;
    }

    // ---- Section 6: category changes affect existing vehicles ----

    [Fact]
    public async Task Section6_vehicle_category_follows_a_boundary_change_made_via_bulk_edit()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        Assert.Equal("Medium", await CategoryOfJohnAsync(client));   // 2200 kg in [500, 2500)

        var response = await client.PutAsJsonAsync("/api/categories/bulk",
            new BulkCategoryRequest(new List<UpsertCategoryRequest>
            {
                new("Light",  0m,    500m,  "light.svg"),
                new("Medium", 500m,  2000m, "medium.svg"),
                new("Heavy",  2000m, null,  "heavy.svg"),
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Heavy", await CategoryOfJohnAsync(client));    // now in [2000, ∞)
    }

    // ---- Create (split) ----

    [Fact]
    public async Task Creating_a_category_splits_an_existing_one_and_recategorises_vehicles()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Mid-heavy", 2000m, "van.svg"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var all = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.Equal(4, all!.Count);
        Assert.Equal(2000m, all.Single(c => c.Name == "Medium").MaxWeightKg);

        Assert.Equal("Mid-heavy", await CategoryOfJohnAsync(client)); // [2000, 2500)
    }

    [Fact]
    public async Task Creating_a_category_at_an_existing_boundary_is_rejected()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Dup", 500m, "car.svg"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_icon_is_rejected_by_the_server()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("X", 1000m, "../../evil.svg"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Delete (merge) ----

    [Fact]
    public async Task Deleting_a_category_merges_its_range_into_the_one_below()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.DeleteAsync("/api/categories/2");   // Medium
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var all = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.Equal(2, all!.Count);
        Assert.Equal(2500m, all.Single(c => c.Name == "Light").MaxWeightKg);

        Assert.Equal("Light", await CategoryOfJohnAsync(client));        // Light now [0, 2500)
    }

    [Fact]
    public async Task Deleting_a_missing_category_returns_404()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.DeleteAsync("/api/categories/99");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Update ----

    [Fact]
    public async Task Editing_a_missing_category_returns_404()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PutAsJsonAsync("/api/categories/99",
            new UpsertCategoryRequest("X", 0m, 10m, "car.svg"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Vehicle validation reaches the API as 400, not 500 ----

    [Fact]
    public async Task Vehicle_with_excessive_weight_returns_400_not_500()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/vehicles",
            new CreateVehicleRequest("Jane", 1, 2020, 999_999_999m));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}