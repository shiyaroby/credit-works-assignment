using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditWorks.Tests;

public class CategoryChangeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CategoryChangeIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false) ||
                        (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false))
                    .ToList();

                foreach (var d in toRemove) services.Remove(d);

                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseInMemoryDatabase("test-" + Guid.NewGuid()));
            });
        });
    }

    [Fact]
    public async Task Changing_category_ranges_updates_existing_vehicle_category()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new CategoryResolver();

        db.VehicleCategories.AddRange(
            new VehicleCategory { Id = 1, Name = "Light",  MinWeightKg = 0m,    MaxWeightKg = 500m,  IconName = "light.svg" },
            new VehicleCategory { Id = 2, Name = "Medium", MinWeightKg = 500m,  MaxWeightKg = 2500m, IconName = "medium.svg" },
            new VehicleCategory { Id = 3, Name = "Heavy",  MinWeightKg = 2500m, MaxWeightKg = null,  IconName = "heavy.svg" });

        var vehicle = new Vehicle
        {
            OwnerName = "John Smith",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            WeightKg = 2200m
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        var first = resolver.ResolveCategory(vehicle.WeightKg, db.VehicleCategories.ToList());
        Assert.NotNull(first);
        Assert.Equal("Medium", first!.Name);

        var medium = db.VehicleCategories.First(c => c.Name == "Medium");
        var heavy  = db.VehicleCategories.First(c => c.Name == "Heavy");
        medium.MaxWeightKg = 2000m;
        heavy.MinWeightKg  = 2000m;
        await db.SaveChangesAsync();

        var second = resolver.ResolveCategory(vehicle.WeightKg, db.VehicleCategories.ToList());
        Assert.NotNull(second);
        Assert.Equal("Heavy", second!.Name);
    }
}