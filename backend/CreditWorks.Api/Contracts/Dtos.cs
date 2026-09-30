namespace CreditWorks.Api.Contracts;

public record VehicleDto(int Id, string OwnerName, int ManufacturerId,
    string ManufacturerName, int YearOfManufacture, decimal WeightKg,
    int? CategoryId, string? CategoryName, string? CategoryIcon);

public record CreateVehicleRequest(string OwnerName, int ManufacturerId,
    int YearOfManufacture, decimal? WeightKg);

public record CategoryDto(int Id, string Name, decimal MinWeightKg,
    decimal? MaxWeightKg, string IconName);

public record UpsertCategoryRequest(string Name, decimal MinWeightKg,
    decimal? MaxWeightKg, string IconName);

public record BulkCategoryRequest(IReadOnlyList<UpsertCategoryRequest> Categories);

public record CreateCategoryRequest(string Name, decimal MinWeightKg, string IconName);