namespace CreditWorks.Core.Models;

public class Manufacturer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}