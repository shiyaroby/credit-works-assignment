using CreditWorks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Vehicle>(e =>
        {
            e.Property(v => v.OwnerName).IsRequired().HasMaxLength(200);
            e.Property(v => v.WeightKg).HasColumnType("decimal(10,2)");
            e.HasOne(v => v.Manufacturer).WithMany(m => m.Vehicles)
             .HasForeignKey(v => v.ManufacturerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Manufacturer>(e =>
        {
            e.Property(m => m.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(m => m.Name).IsUnique();
        });

        b.Entity<VehicleCategory>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.IconName).IsRequired().HasMaxLength(100);
            e.Property(c => c.MinWeightKg).HasColumnType("decimal(10,2)");
            e.Property(c => c.MaxWeightKg).HasColumnType("decimal(10,2)");
        });

        b.Entity<Manufacturer>().HasData(
            new Manufacturer { Id = 1, Name = "Mazda" },
            new Manufacturer { Id = 2, Name = "Mercedes" },
            new Manufacturer { Id = 3, Name = "Honda" },
            new Manufacturer { Id = 4, Name = "Ferrari" },
            new Manufacturer { Id = 5, Name = "Toyota" });

        b.Entity<VehicleCategory>().HasData(
            new VehicleCategory { Id = 1, Name = "Light",  MinWeightKg = 0m,    MaxWeightKg = 500m,  IconName = "light.svg" },
            new VehicleCategory { Id = 2, Name = "Medium", MinWeightKg = 500m,  MaxWeightKg = 2500m, IconName = "medium.svg" },
            new VehicleCategory { Id = 3, Name = "Heavy",  MinWeightKg = 2500m, MaxWeightKg = null,  IconName = "heavy.svg" });
    }
}