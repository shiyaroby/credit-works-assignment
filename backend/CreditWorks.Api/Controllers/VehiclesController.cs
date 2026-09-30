using CreditWorks.Api.Contracts;
using CreditWorks.Core.Interfaces;
using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
using CreditWorks.Core.Validation;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICategoryResolver _resolver;

    public VehiclesController(AppDbContext db, ICategoryResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetAll(
        [FromQuery] string? sortBy = "ownerName",
        [FromQuery] string? dir = "asc")
    {
        var vehicles = await _db.Vehicles.AsNoTracking()
            .Include(v => v.Manufacturer)
            .ToListAsync();

        var sorted = VehicleSorter.Sort(vehicles, sortBy, dir).ToList();

        var categories = await _db.VehicleCategories.AsNoTracking().ToListAsync();

        var projected = sorted.Select(v =>
        {
            var cat = _resolver.ResolveCategory(v.WeightKg, categories);
            return new VehicleDto(v.Id, v.OwnerName, v.ManufacturerId,
                v.Manufacturer.Name, v.YearOfManufacture, v.WeightKg,
                cat?.Id, cat?.Name, cat?.IconName);
        });

        return Ok(projected);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create(CreateVehicleRequest req)
    {
        if (req.WeightKg is null)
            return BadRequest(new { errors = new[] { "Weight is required." } });

        var errors = VehicleValidator.Validate(req.OwnerName, req.ManufacturerId,
            req.YearOfManufacture, req.WeightKg.Value);
        if (errors.Any()) return BadRequest(new { errors });

        var mfr = await _db.Manufacturers
            .FirstOrDefaultAsync(m => m.Id == req.ManufacturerId && m.IsActive);
        if (mfr is null)
            return BadRequest(new { errors = new[] { "Manufacturer not found or inactive." } });

        var vehicle = new Vehicle
        {
            OwnerName = req.OwnerName.Trim(),
            ManufacturerId = req.ManufacturerId,
            YearOfManufacture = req.YearOfManufacture,
            WeightKg = req.WeightKg.Value
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();

        var categories = await _db.VehicleCategories.ToListAsync();
        var cat = _resolver.ResolveCategory(vehicle.WeightKg, categories);

        return CreatedAtAction(nameof(GetAll), new { id = vehicle.Id },
            new VehicleDto(vehicle.Id, vehicle.OwnerName, vehicle.ManufacturerId,
                mfr.Name, vehicle.YearOfManufacture, vehicle.WeightKg,
                cat?.Id, cat?.Name, cat?.IconName));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.Vehicles.FindAsync(id);
        if (v is null) return NotFound();
        _db.Vehicles.Remove(v);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}