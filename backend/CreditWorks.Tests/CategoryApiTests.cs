using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreditWorks.Api.Contracts;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Creating_a_vehicle_without_weight_returns_weight_required()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/vehicles", new
        {
            ownerName = "Jane",
            manufacturerId = 1,
            yearOfManufacture = 2020
            // weightKg omitted
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("Weight is required"));
    }

    [Fact]
    public async Task Creating_a_vehicle_with_inactive_manufacturer_returns_400()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        // Deactivate the seeded manufacturer
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<CreditWorks.Infrastructure.Data.AppDbContext>();
            var m = await db.Manufacturers.FindAsync(1);
            m!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/vehicles",
            new CreateVehicleRequest("Jane", 1, 2020, 1500m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("inactive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Malformed_json_returns_400_with_a_message()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        // "abc" cannot deserialize into decimal? -> ModelState error with empty ErrorMessage
        var content = new StringContent(
            """{"ownerName":"Jane","manufacturerId":1,"yearOfManufacture":2020,"weightKg":"abc"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/vehicles", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString()).ToList();

        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e)));
        Assert.DoesNotContain(errors, e => e!.Contains("/Users/") || e.Contains(".cs:"));
    }

    [Fact]
    public async Task Creating_a_category_with_a_duplicate_name_returns_400()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        // "Medium" already exists. Try creating another.
        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Medium", 1000m, "car.svg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Renaming_a_category_to_an_existing_name_returns_400()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PutAsJsonAsync("/api/categories/2",
            new UpsertCategoryRequest("Light", 500m, 2500m, "medium.svg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_replace_rejects_case_variant_duplicate_names()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        // Ranges are valid; only the names collide ("Light" vs "light").
        var response = await client.PutAsJsonAsync("/api/categories/bulk",
            new BulkCategoryRequest(new List<UpsertCategoryRequest>
            {
                new("Light", 0m,    500m,  "light.svg"),
                new("light", 500m,  2500m, "medium.svg"),
                new("Heavy", 2500m, null,  "heavy.svg"),
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("Duplicate category name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rejected_bulk_replace_leaves_existing_categories_unchanged()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var response = await client.PutAsJsonAsync("/api/categories/bulk",
            new BulkCategoryRequest(new List<UpsertCategoryRequest>
            {
            new("Light",  0m,    500m,  "light.svg"),
            new("Medium", 500m,  2000m, "medium.svg"),
            new("Medium", 2000m, null,  "heavy.svg"),   // repeated name
            }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var all = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.NotNull(all);
        Assert.Equal(2500m, all!.Single(c => c.Name == "Heavy").MinWeightKg);
        Assert.Equal("Medium", await CategoryOfJohnAsync(client));
    }
}