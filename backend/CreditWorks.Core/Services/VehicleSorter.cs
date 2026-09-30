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
            ("manufacturer", true) => vehicles.OrderByDescending(v => v.Manufacturer.Name).ThenBy(v => v.Id),
            ("manufacturer", false) => vehicles.OrderBy(v => v.Manufacturer.Name).ThenBy(v => v.Id),
            ("year", true) => vehicles.OrderByDescending(v => v.YearOfManufacture).ThenBy(v => v.Id),
            ("year", false) => vehicles.OrderBy(v => v.YearOfManufacture).ThenBy(v => v.Id),
            ("weight", true) => vehicles.OrderByDescending(v => v.WeightKg).ThenBy(v => v.Id),
            ("weight", false) => vehicles.OrderBy(v => v.WeightKg).ThenBy(v => v.Id),
            ("ownername", true) => vehicles.OrderByDescending(v => v.OwnerName).ThenBy(v => v.Id),
            _ => vehicles.OrderBy(v => v.OwnerName).ThenBy(v => v.Id)
        };
    }
}