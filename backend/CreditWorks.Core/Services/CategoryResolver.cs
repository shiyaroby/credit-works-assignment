using CreditWorks.Core.Interfaces;
using CreditWorks.Core.Models;

namespace CreditWorks.Core.Services;

public class CategoryResolver : ICategoryResolver
{
    public VehicleCategory? ResolveCategory(decimal weightKg, IEnumerable<VehicleCategory> categories)
        => categories.FirstOrDefault(c =>
            weightKg >= c.MinWeightKg &&
            (c.MaxWeightKg == null || weightKg < c.MaxWeightKg.Value));
}