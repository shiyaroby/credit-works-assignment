using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
using CreditWorks.Core.Validation;
using Xunit;

namespace CreditWorks.Tests;

public class CategoryRangeEditorTests
{
    private static List<VehicleCategory> Defaults() => new()
    {
        new() { Id = 1, Name = "Light",  MinWeightKg = 0m,    MaxWeightKg = 500m,  IconName = "light.svg" },
        new() { Id = 2, Name = "Medium", MinWeightKg = 500m,  MaxWeightKg = 2500m, IconName = "medium.svg" },
        new() { Id = 3, Name = "Heavy",  MinWeightKg = 2500m, MaxWeightKg = null,  IconName = "heavy.svg" },
    };

    private static void AssertStillValid(List<VehicleCategory> cats) =>
        Assert.Empty(CategoryRangeValidator.Validate(cats));

    // ---- Split (create) ----
    [Fact]
    public void Split_inside_a_category_shortens_it_and_creates_the_new_upper_part()
    {
        var cats = Defaults();
        var result = CategoryRangeEditor.Split(cats, "Mid-heavy", "van.svg", 1000m);

        Assert.True(result.Succeeded);
        Assert.Equal(1000m, cats.Single(c => c.Name == "Medium").MaxWeightKg);
        Assert.Equal(1000m, result.Category!.MinWeightKg);
        Assert.Equal(2500m, result.Category.MaxWeightKg);
        AssertStillValid(cats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(2500)]
    public void Split_at_an_existing_boundary_is_rejected(decimal splitAt)
    {
        var cats = Defaults();
        Assert.False(CategoryRangeEditor.Split(cats, "X", "car.svg", splitAt).Succeeded);
    }

    [Fact]
    public void Split_inside_the_unbounded_top_category_keeps_the_new_top_unbounded()
    {
        var cats = Defaults();
        var result = CategoryRangeEditor.Split(cats, "Super", "truck.svg", 5000m);

        Assert.True(result.Succeeded);
        Assert.Null(result.Category!.MaxWeightKg);
        Assert.Equal(5000m, cats.Single(c => c.Name == "Heavy").MaxWeightKg);
        AssertStillValid(cats);
    }

    // ---- Remove (delete) ----
    [Fact]
    public void Remove_middle_category_is_absorbed_by_the_one_below()
    {
        var cats = Defaults();
        var result = CategoryRangeEditor.Remove(cats, 2);

        Assert.True(result.Succeeded);
        Assert.Equal(2500m, cats.Single(c => c.Name == "Light").MaxWeightKg);
        AssertStillValid(cats);
    }

    [Fact]
    public void Remove_lowest_category_is_absorbed_by_the_one_above()
    {
        var cats = Defaults();
        Assert.True(CategoryRangeEditor.Remove(cats, 1).Succeeded);
        Assert.Equal(0m, cats.Single(c => c.Name == "Medium").MinWeightKg);
        AssertStillValid(cats);
    }

    [Fact]
    public void Remove_top_category_makes_the_one_below_unbounded()
    {
        var cats = Defaults();
        Assert.True(CategoryRangeEditor.Remove(cats, 3).Succeeded);
        Assert.Null(cats.Single(c => c.Name == "Medium").MaxWeightKg);
        AssertStillValid(cats);
    }

    [Fact]
    public void Remove_only_category_is_rejected()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Id = 1, Name = "Only", MinWeightKg = 0m, MaxWeightKg = null, IconName = "car.svg" }
        };
        Assert.False(CategoryRangeEditor.Remove(cats, 1).Succeeded);
    }

    [Fact]
    public void Remove_reports_not_found() =>
        Assert.True(CategoryRangeEditor.Remove(Defaults(), 99).NotFound);
}