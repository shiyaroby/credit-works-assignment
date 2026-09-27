using CreditWorks.Core.Models;

namespace CreditWorks.Core.Services;

public static class VehicleSorter
{
    /// <summary>
    /// Sorts a sequence of vehicles by one of four fields in ascending or
    /// descending order. Falls back to owner name ascending for unknown input.
    /// </summary>
    public static IEnumerable<Vehicle> Sort(
        IEnumerable<Vehicle> vehicles,
        string? sortBy,
        string? dir)
    {
        var descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("manufacturer", true) => vehicles.OrderByDescending(v => v.Manufacturer.Name),
            ("manufacturer", false) => vehicles.OrderBy(v => v.Manufacturer.Name),
            ("year", true) => vehicles.OrderByDescending(v => v.YearOfManufacture),
            ("year", false) => vehicles.OrderBy(v => v.YearOfManufacture),
            ("weight", true) => vehicles.OrderByDescending(v => v.WeightKg),
            ("weight", false) => vehicles.OrderBy(v => v.WeightKg),
            ("ownername", true) => vehicles.OrderByDescending(v => v.OwnerName),
            _ => vehicles.OrderBy(v => v.OwnerName)
        };
    }
}