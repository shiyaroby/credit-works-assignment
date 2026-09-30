namespace CreditWorks.Core.Validation;

public static class VehicleValidator
{
    public const int MaxOwnerNameLength = 200;
    public const decimal MaxWeightKg = 99_999_999.99m; // decimal(10,2)
    public const int FirstAutomobileYear = 1886;

    public static IReadOnlyList<string> Validate(
        string ownerName, int manufacturerId, int year, decimal weightKg,
        TimeProvider? timeProvider = null)
    {
        var e = new List<string>();
        var maxYear = (timeProvider ?? TimeProvider.System).GetUtcNow().Year + 1;

        if (string.IsNullOrWhiteSpace(ownerName)) e.Add("Owner's name is required.");
        else if (ownerName.Trim().Length > MaxOwnerNameLength)
            e.Add($"Owner's name must be {MaxOwnerNameLength} characters or fewer.");

        if (manufacturerId <= 0) e.Add("Manufacturer is required.");
        if (year < FirstAutomobileYear || year > maxYear)
            e.Add($"Year must be between {FirstAutomobileYear} and {maxYear}.");

        if (weightKg <= 0) e.Add("Weight must be positive.");
        else if (weightKg > MaxWeightKg) e.Add($"Weight cannot exceed {MaxWeightKg:N2} kg.");
        if (decimal.Round(weightKg, 2) != weightKg)
            e.Add("Weight supports at most two decimal places.");

        return e;
    }
}