using CreditWorks.Core.Models;
using CreditWorks.Core.Validation;
using Xunit;

namespace CreditWorks.Tests;

public class CategoryRangeValidatorTests
{
    [Fact]
    public void Accepts_valid_defaults()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Light",  MinWeightKg = 0,    MaxWeightKg = 500 },
            new() { Name = "Medium", MinWeightKg = 500,  MaxWeightKg = 2500 },
            new() { Name = "Heavy",  MinWeightKg = 2500, MaxWeightKg = null },
        };
        Assert.Empty(CategoryRangeValidator.Validate(cats));
    }

    [Fact]
    public void Detects_gap_between_ranges()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Light",  MinWeightKg = 0,    MaxWeightKg = 500 },
            new() { Name = "Medium", MinWeightKg = 600,  MaxWeightKg = 2500 },
            new() { Name = "Heavy",  MinWeightKg = 2500, MaxWeightKg = null },
        };
        var errors = CategoryRangeValidator.Validate(cats);
        Assert.Contains(errors, e => e.Contains("Gap"));
    }

    [Fact]
    public void Detects_overlap_between_ranges()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Light",  MinWeightKg = 0,    MaxWeightKg = 600 },
            new() { Name = "Medium", MinWeightKg = 500,  MaxWeightKg = 2500 },
            new() { Name = "Heavy",  MinWeightKg = 2500, MaxWeightKg = null },
        };
        var errors = CategoryRangeValidator.Validate(cats);
        Assert.Contains(errors, e => e.Contains("Overlap"));
    }

    [Fact]
    public void Detects_missing_zero_start()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Light",  MinWeightKg = 100,  MaxWeightKg = 500 },
            new() { Name = "Heavy",  MinWeightKg = 500,  MaxWeightKg = null },
        };
        var errors = CategoryRangeValidator.Validate(cats);
        Assert.Contains(errors, e => e.Contains("start at 0"));
    }

    [Fact]
    public void Requires_top_category_unbounded()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Only", MinWeightKg = 0, MaxWeightKg = 100 },
        };
        var errors = CategoryRangeValidator.Validate(cats);
        Assert.Contains(errors, e => e.Contains("unbounded"));
    }

    [Fact]
    public void Detects_unbounded_non_top_category()
    {
        var cats = new List<VehicleCategory>
        {
            new() { Name = "Light", MinWeightKg = 0, MaxWeightKg = null },
            new() { Name = "Heavy", MinWeightKg = 500, MaxWeightKg = null },
        };
        var errors = CategoryRangeValidator.Validate(cats);
        Assert.Contains(errors, e => e.Contains("not the highest"));
    }

    [Fact]
    public void Rejects_empty_collection()
    {
        var errors = CategoryRangeValidator.Validate(Array.Empty<VehicleCategory>());
        Assert.Single(errors);
        Assert.Contains("At least one", errors[0]);
    }
}