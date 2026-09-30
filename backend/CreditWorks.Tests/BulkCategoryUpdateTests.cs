using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreditWorks.Api.Contracts;
using Xunit;

namespace CreditWorks.Tests;

public class BulkCategoryUpdateTests
{
    private static async Task<(ApiFactory factory, HttpClient client)> StartAsync()
    {
        var factory = new ApiFactory();
        await factory.SeedAsync();
        return (factory, factory.CreateClient());
    }

    [Fact]
    public async Task Bulk_replace_can_move_a_shared_boundary_in_one_call()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

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
        var (factory, client) = await StartAsync();
        using var _ = factory;

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
        var (factory, client) = await StartAsync();
        using var _ = factory;

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
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var payload = new BulkCategoryRequest(new List<UpsertCategoryRequest>());
        var response = await client.PutAsJsonAsync("/api/categories/bulk", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_shared_boundary_returns_409_with_bulk_hint()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var payload = new UpsertCategoryRequest("Medium", 500m, 2000m, "medium.svg");
        var response = await client.PutAsJsonAsync("/api/categories/2", payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("bulk-edit", body.GetProperty("suggestedAction").GetString());
    }

    [Fact]
    public async Task Renaming_a_category_still_succeeds()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        var payload = new UpsertCategoryRequest("Midweight", 500m, 2500m, "medium.svg");
        var response = await client.PutAsJsonAsync("/api/categories/2", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}