namespace CreditWorks.Core.Models;

public class VehicleCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinWeightKg { get; set; }   // inclusive
    public decimal? MaxWeightKg { get; set; }  // exclusive; null = unbounded (top)
    public string IconName { get; set; } = string.Empty;
}