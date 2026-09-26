using CreditWorks.Core.Validation;
using Xunit;

namespace CreditWorks.Tests;

public class VehicleValidatorTests
{
    private const int ValidYear = 2020;

    [Fact]
    public void Rejects_empty_owner() =>
        Assert.Contains(VehicleValidator.Validate("", 1, ValidYear, 100m),
            e => e.Contains("Owner"));

    [Fact]
    public void Rejects_whitespace_owner() =>
        Assert.Contains(VehicleValidator.Validate("   ", 1, ValidYear, 100m),
            e => e.Contains("Owner"));

    [Fact]
    public void Rejects_missing_manufacturer() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 0, ValidYear, 100m),
            e => e.Contains("Manufacturer"));

    [Fact]
    public void Rejects_year_before_1886() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 1, 1800, 100m),
            e => e.Contains("Year"));

    [Fact]
    public void Rejects_far_future_year() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 1, 9999, 100m),
            e => e.Contains("Year"));

    [Fact]
    public void Rejects_zero_weight() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 1, ValidYear, 0m),
            e => e.Contains("positive"));

    [Fact]
    public void Rejects_negative_weight() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 1, ValidYear, -5m),
            e => e.Contains("positive"));

    [Fact]
    public void Rejects_three_decimal_places() =>
        Assert.Contains(VehicleValidator.Validate("Alice", 1, ValidYear, 100.123m),
            e => e.Contains("two decimal"));

    [Fact]
    public void Accepts_two_decimals() =>
        Assert.Empty(VehicleValidator.Validate("Alice", 1, ValidYear, 1850.75m));

    [Fact]
    public void Accepts_integer_weight() =>
        Assert.Empty(VehicleValidator.Validate("Alice", 1, ValidYear, 2000m));
}