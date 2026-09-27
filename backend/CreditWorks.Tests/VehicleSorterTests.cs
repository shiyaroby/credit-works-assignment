using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
using Xunit;

namespace CreditWorks.Tests;

public class VehicleSorterTests
{
    private static List<Vehicle> Fixture() => new()
    {
        new Vehicle { Id = 1, OwnerName = "Charlie", ManufacturerId = 1,
                      Manufacturer = new Manufacturer { Id = 1, Name = "Toyota" },
                      YearOfManufacture = 2020, WeightKg = 1800m },
        new Vehicle { Id = 2, OwnerName = "Alice",   ManufacturerId = 2,
                      Manufacturer = new Manufacturer { Id = 2, Name = "Mazda" },
                      YearOfManufacture = 2015, WeightKg = 900m },
        new Vehicle { Id = 3, OwnerName = "Bob",     ManufacturerId = 3,
                      Manufacturer = new Manufacturer { Id = 3, Name = "Ferrari" },
                      YearOfManufacture = 2023, WeightKg = 2600m },
    };

    [Fact]
    public void Sorts_by_owner_ascending_by_default()
    {
        var result = VehicleSorter.Sort(Fixture(), "ownerName", "asc").ToList();
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.Select(v => v.OwnerName));
    }

    [Fact]
    public void Sorts_by_owner_descending()
    {
        var result = VehicleSorter.Sort(Fixture(), "ownerName", "desc").ToList();
        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, result.Select(v => v.OwnerName));
    }

    [Fact]
    public void Sorts_by_manufacturer_ascending()
    {
        var result = VehicleSorter.Sort(Fixture(), "manufacturer", "asc").ToList();
        Assert.Equal(new[] { "Ferrari", "Mazda", "Toyota" },
            result.Select(v => v.Manufacturer.Name));
    }

    [Fact]
    public void Sorts_by_manufacturer_descending()
    {
        var result = VehicleSorter.Sort(Fixture(), "manufacturer", "desc").ToList();
        Assert.Equal(new[] { "Toyota", "Mazda", "Ferrari" },
            result.Select(v => v.Manufacturer.Name));
    }

    [Fact]
    public void Sorts_by_year_ascending()
    {
        var result = VehicleSorter.Sort(Fixture(), "year", "asc").ToList();
        Assert.Equal(new[] { 2015, 2020, 2023 }, result.Select(v => v.YearOfManufacture));
    }

    [Fact]
    public void Sorts_by_year_descending()
    {
        var result = VehicleSorter.Sort(Fixture(), "year", "desc").ToList();
        Assert.Equal(new[] { 2023, 2020, 2015 }, result.Select(v => v.YearOfManufacture));
    }

    [Fact]
    public void Sorts_by_weight_ascending()
    {
        var result = VehicleSorter.Sort(Fixture(), "weight", "asc").ToList();
        Assert.Equal(new[] { 900m, 1800m, 2600m }, result.Select(v => v.WeightKg));
    }

    [Fact]
    public void Sorts_by_weight_descending()
    {
        var result = VehicleSorter.Sort(Fixture(), "weight", "desc").ToList();
        Assert.Equal(new[] { 2600m, 1800m, 900m }, result.Select(v => v.WeightKg));
    }

    [Fact]
    public void Falls_back_to_owner_ascending_for_unknown_field()
    {
        var result = VehicleSorter.Sort(Fixture(), "nonsense", "asc").ToList();
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.Select(v => v.OwnerName));
    }

    [Fact]
    public void Falls_back_to_owner_ascending_when_sortBy_is_null()
    {
        var result = VehicleSorter.Sort(Fixture(), null, null).ToList();
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.Select(v => v.OwnerName));
    }

    [Fact]
    public void Sort_is_case_insensitive_on_field_name()
    {
        var result = VehicleSorter.Sort(Fixture(), "OWNERNAME", "DESC").ToList();
        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, result.Select(v => v.OwnerName));
    }

    [Fact]
    public void Returns_empty_for_empty_input()
    {
        var result = VehicleSorter.Sort(Array.Empty<Vehicle>(), "weight", "asc").ToList();
        Assert.Empty(result);
    }
}