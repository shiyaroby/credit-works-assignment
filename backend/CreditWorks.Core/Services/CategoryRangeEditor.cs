using CreditWorks.Core.Models;
using CreditWorks.Core.Validation;

namespace CreditWorks.Core.Services;

public sealed record CategoryEditResult(
    VehicleCategory? Category, IReadOnlyList<string> Errors, bool NotFound = false)
{
    public bool Succeeded => !NotFound && Errors.Count == 0;

    public static CategoryEditResult Ok(VehicleCategory c) => new(c, Array.Empty<string>());
    public static CategoryEditResult Fail(IReadOnlyList<string> errors) => new(null, errors);
    public static CategoryEditResult Fail(string error) => new(null, new[] { error });
    public static CategoryEditResult Missing() => new(null, Array.Empty<string>(), true);
}

/// <summary>
/// Applies single-category edits while keeping the set gap-free and overlap-free.
/// Mutates the supplied list/entities in memory; the caller persists only on success.
/// </summary>
public static class CategoryRangeEditor
{
    /// <summary>
    /// Adds a category starting at <paramref name="splitAt"/>. It takes over the upper part of
    /// the existing category containing that weight, which is shortened to end at the split.
    /// </summary>
    public static CategoryEditResult Split(
        IList<VehicleCategory> categories, string name, string iconName, decimal splitAt)
    {
        var host = categories.FirstOrDefault(c =>
            splitAt > c.MinWeightKg && (c.MaxWeightKg == null || splitAt < c.MaxWeightKg.Value));

        if (host is null)
            return CategoryEditResult.Fail(
                "A new category's minimum weight must fall strictly inside an existing category's " +
                "range, because it splits that category in two.");

        var created = new VehicleCategory
        {
            Name = name,
            IconName = iconName,
            MinWeightKg = splitAt,
            MaxWeightKg = host.MaxWeightKg
        };
        host.MaxWeightKg = splitAt;
        categories.Add(created);

        return Finish(categories, created);
    }

    /// <summary>
    /// Removes a category; its range is absorbed by the category below it
    /// (or by the one above it when the lowest category is removed).
    /// </summary>
    public static CategoryEditResult Remove(IList<VehicleCategory> categories, int id)
    {
        var ordered = categories.OrderBy(c => c.MinWeightKg).ToList();
        var index = ordered.FindIndex(c => c.Id == id);
        if (index < 0) return CategoryEditResult.Missing();

        if (ordered.Count == 1)
            return CategoryEditResult.Fail("Cannot delete the only category. Add a replacement first.");

        var target = ordered[index];
        if (index > 0) ordered[index - 1].MaxWeightKg = target.MaxWeightKg;
        else ordered[1].MinWeightKg = target.MinWeightKg;

        categories.Remove(target);
        return Finish(categories, target);
    }

    private static CategoryEditResult Finish(IEnumerable<VehicleCategory> all, VehicleCategory subject)
    {
        var errors = CategoryRangeValidator.Validate(all);
        return errors.Count == 0 ? CategoryEditResult.Ok(subject) : CategoryEditResult.Fail(errors);
    }
}