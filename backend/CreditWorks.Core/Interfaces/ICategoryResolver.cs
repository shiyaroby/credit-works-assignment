using CreditWorks.Core.Models;

namespace CreditWorks.Core.Interfaces;

public interface ICategoryResolver
{
    VehicleCategory? ResolveCategory(decimal weightKg, IEnumerable<VehicleCategory> categories);
}