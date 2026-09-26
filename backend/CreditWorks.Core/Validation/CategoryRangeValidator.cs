using CreditWorks.Core.Models;

namespace CreditWorks.Core.Validation;

public static class CategoryRangeValidator
{
    public static IReadOnlyList<string> Validate(IEnumerable<VehicleCategory> categories)
    {
        var errors = new List<string>();
        var ordered = categories.OrderBy(c => c.MinWeightKg).ToList();

        if (ordered.Count == 0)
            return new[] { "At least one category is required." };

        if (ordered[0].MinWeightKg != 0m)
            errors.Add("The lowest category must start at 0 kg.");

        for (int i = 0; i < ordered.Count; i++)
        {
            var c = ordered[i];
            if (c.MaxWeightKg.HasValue && c.MaxWeightKg.Value <= c.MinWeightKg)
                errors.Add($"Category '{c.Name}': max weight must exceed min weight.");

            if (i < ordered.Count - 1)
            {
                var next = ordered[i + 1];

                if (c.MaxWeightKg == null)
                    errors.Add($"Category '{c.Name}' is unbounded but is not the highest.");
                else if (c.MaxWeightKg.Value < next.MinWeightKg)
                    errors.Add($"Gap between '{c.Name}' and '{next.Name}'.");
                else if (c.MaxWeightKg.Value > next.MinWeightKg)
                    errors.Add($"Overlap between '{c.Name}' and '{next.Name}'.");
            }
        }

        if (ordered[^1].MaxWeightKg != null)
            errors.Add("The highest category must be unbounded.");

        return errors;
    }
}