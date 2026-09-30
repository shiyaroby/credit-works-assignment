using CreditWorks.Core.Models;

namespace CreditWorks.Core.Validation;

/// <summary>Validates single-category fields (not cross-category range rules).</summary>
public static class CategoryFieldValidator
{
    public const int MaxNameLength = 100;
    public const decimal MaxWeightKg = 99_999_999.99m; // decimal(10,2)

    public static IReadOnlyList<string> Validate(
        string? name, string? iconName, decimal minWeightKg, decimal? maxWeightKg)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Category name is required.");
        else if (name.Trim().Length > MaxNameLength)
            errors.Add($"Category name must be {MaxNameLength} characters or fewer.");

        if (string.IsNullOrWhiteSpace(iconName))
            errors.Add("Category icon is required.");
        else if (!CategoryIcons.IsKnown(iconName.Trim()))
            errors.Add("Category icon is not recognised.");

        CheckWeight("Minimum weight", minWeightKg, errors);

        if (maxWeightKg.HasValue)
        {
            CheckWeight("Maximum weight", maxWeightKg.Value, errors);
            if (maxWeightKg.Value <= minWeightKg)
                errors.Add("Maximum weight must be greater than minimum weight.");
        }

        return errors;
    }

    private static void CheckWeight(string label, decimal value, List<string> errors)
    {
        if (value < 0) errors.Add($"{label} cannot be negative.");
        if (value > MaxWeightKg) errors.Add($"{label} cannot exceed {MaxWeightKg:N2} kg.");
        if (decimal.Round(value, 2) != value) errors.Add($"{label} supports at most two decimal places.");
    }
    public static IReadOnlyList<string> ValidateNameUnique(
        string name, int? excludeId, IEnumerable<VehicleCategory> existing)
    {
        var trimmed = name.Trim();
        var collision = existing.Any(c =>
            c.Id != excludeId &&
            string.Equals(c.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        return collision
            ? new[] { $"A category named '{trimmed}' already exists." }
            : Array.Empty<string>();
    }

}