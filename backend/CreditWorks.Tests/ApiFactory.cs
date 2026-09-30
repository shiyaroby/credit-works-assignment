using CreditWorks.Core.Models;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CreditWorks.Tests;

/// <summary>Hosts the real API with an isolated InMemory database per instance.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "api-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false) ||
                (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(_dbName));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Manufacturers.Add(new Manufacturer { Id = 1, Name = "Mazda" });
        db.VehicleCategories.AddRange(
            new VehicleCategory { Id = 1, Name = "Light", MinWeightKg = 0m, MaxWeightKg = 500m, IconName = "light.svg" },
            new VehicleCategory { Id = 2, Name = "Medium", MinWeightKg = 500m, MaxWeightKg = 2500m, IconName = "medium.svg" },
            new VehicleCategory { Id = 3, Name = "Heavy", MinWeightKg = 2500m, MaxWeightKg = null, IconName = "heavy.svg" });
        db.Vehicles.Add(new Vehicle
        {
            OwnerName = "John Smith",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            WeightKg = 2200m
        });
        await db.SaveChangesAsync();
    }
}