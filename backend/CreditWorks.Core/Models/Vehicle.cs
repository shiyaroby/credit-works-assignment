namespace CreditWorks.Core.Models;

public class Vehicle
{
    public int Id { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ManufacturerId { get; set; }
    public Manufacturer Manufacturer { get; set; } = null!;
    public int YearOfManufacture { get; set; }
    public decimal WeightKg { get; set; }
    // Category is intentionally NOT stored — computed from current ranges.
}