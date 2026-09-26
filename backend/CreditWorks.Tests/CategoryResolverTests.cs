using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
using Xunit;

namespace CreditWorks.Tests;

public class CategoryResolverTests
{
    private static List<VehicleCategory> Defaults() => new()
    {
        new() { Id = 1, Name = "Light",  MinWeightKg = 0m,    MaxWeightKg = 500m },
        new() { Id = 2, Name = "Medium", MinWeightKg = 500m,  MaxWeightKg = 2500m },
        new() { Id = 3, Name = "Heavy",  MinWeightKg = 2500m, MaxWeightKg = null },
    };

    [Theory]
    [InlineData(0,       "Light")]
    [InlineData(0.01,    "Light")]
    [InlineData(499.99,  "Light")]
    [InlineData(500,     "Medium")]  // boundary → Medium (min inclusive)
    [InlineData(500.01,  "Medium")]
    [InlineData(2499.99, "Medium")]
    [InlineData(2500,    "Heavy")]   // boundary → Heavy
    [InlineData(99999,   "Heavy")]
    public void Resolves_correct_category(decimal weight, string expected)
    {
        var resolver = new CategoryResolver();
        var category = resolver.ResolveCategory(weight, Defaults());
        Assert.NotNull(category);
        Assert.Equal(expected, category!.Name);
    }

    [Fact]
    public void Returns_null_when_categories_empty()
    {
        var resolver = new CategoryResolver();
        Assert.Null(resolver.ResolveCategory(100m, Array.Empty<VehicleCategory>()));
    }
}